namespace Xake

[<AutoOpen>]
module XakeScript =
  open System.Threading

  type XakeOptionsType = {
    /// Defines project root folder
    ProjectRoot : string  // TODO DirectoryInfo?
    /// Maximum number of threads to run the rules
    Threads: int

    // custom logger
    CustomLogger: ILogger

    /// Log file and verbosity level
    FileLog: string
    FileLogLevel: Verbosity

    /// Console output verbosity level. Default is Warn
    ConLogLevel: Verbosity
    /// Overrides "want", i.e. target list
    Want: string list

    /// Defines whether `run` should throw exception if script fails
    FailOnError: bool
  }


  type RuleTarget =
      | FilePattern of string | PhonyTarget of string
  type Rule<'ctx> = 
      | FileRule of string * (Artifact -> Action<'ctx,unit>)
      | PhonyRule of string * Action<'ctx,unit>
  type Rules<'ctx> = Rules of Map<RuleTarget, Rule<'ctx>>

  type ExecContext = {
    TaskPool: MailboxProcessor<WorkerPool.ExecMessage>
    Db: MailboxProcessor<Storage.DatabaseApi>
    Throttler: SemaphoreSlim
    Options: XakeOptionsType
    Rules: Rules<ExecContext>
    Logger: ILogger
  }

  /// Main type.
  type XakeScript = XakeScript of XakeOptionsType * Rules<ExecContext>

  /// Default options
  let XakeOptions = {
    ProjectRoot = System.IO.Directory.GetCurrentDirectory()
    Threads = 4
    ConLogLevel = Normal

    CustomLogger = CustomLogger (fun _ -> false) ignore
    FileLog = "build.log"
    FileLogLevel = Chatty
    Want = []
    FailOnError = false
    }

  module private Impl =
    open WorkerPool
    open Storage

    /// Writes the message with formatting to a log
    let writeLog (level:Logging.Level) fmt  =
      let write s = action {
        let! (ctx:ExecContext) = getCtx()
        return ctx.Logger.Log level "%s" s
      }
      Printf.kprintf write fmt


    let makeFileRule  pattern fnRule = FileRule (pattern, fnRule)
    let makePhonyRule name fnRule = PhonyRule (name, fnRule)

    let addRule rule (Rules rules) :Rules<_> = 
      let target = match rule with | FileRule (selector,_) -> (FilePattern selector) | PhonyRule (name,_) -> (PhonyTarget name)
      rules |> Map.add target rule |> Rules

    // locates the rule
    let private locateRule (Rules rules) projectRoot target =
      let matchRule ruleTarget b = 
        match ruleTarget, target with
          |FilePattern pattern, FileTarget file when Fileset.matches pattern projectRoot file.FullName ->
              // writeLog Verbose "Found pattern '%s' for %s" pattern (getShortname target)
              Some (b)
          |PhonyTarget name, PhonyAction phony when phony = name ->
              // writeLog Verbose "Found phony pattern '%s'" name
              Some (b)
          | _ -> None
      rules |> Map.tryPick matchRule

    let private reportError ctx error details =
      do ctx.Logger.Log Error "Error '%s'. See build.log for details" error
      do ctx.Logger.Log Verbose "Error details are:\n%A\n\n" details

    let private raiseError ctx error details =
      do reportError ctx error details
      raise (XakeException(sprintf "Script failed (error code: %A)\n%A" error details))

    // executes single artifact
    let private execOne ctx target =

      // TODO retrieve the status from db
      // if dependencies are not changed skip the step

      target
      |> locateRule ctx.Rules ctx.Options.ProjectRoot
      |> Option.bind (function
          | FileRule (_, action) ->
            let (FileTarget artifact) = target in
            let (Action r) = action artifact in Some r
          | PhonyRule (_, Action r) -> Some r)
      |> function
        | Some action ->
          async {
            let! task = ctx.TaskPool.PostAndAsyncReply (fun chnl ->
              Run(target,
                async {
                  let! (result,_) = action (BuildLog.makeResult target,ctx)
                  do Store result |> ctx.Db.Post |> ignore
                  return ()
                }, chnl))
            return! task
          }
        | None ->   // TODO should always fail for phony
          if not <| exists target then raiseError ctx (sprintf "Neither rule nor file is found for '%s'" (getFullname target)) ""
          async {()}

    /// Executes several artifacts in parallel
    let private exec ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

    // phony actions are detected by their name so if there's "clean" phony and file "clean" in `need` list if will choose first
    let makeTarget ctx name =
      let (Rules rr) = ctx.Rules
      if rr |> Map.containsKey(PhonyTarget name) then
        PhonyAction name
      else
        FileTarget (Artifact (ctx.Options.ProjectRoot </> name))

    /// Executes and awaits specified artifacts
    let needTarget targets = action {
        let! ctx = getCtx()
        ctx.Throttler.Release() |> ignore
        do! targets |> (exec ctx >> Async.Ignore)
        do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ (targets |> List.map BuildLog.Dependency.File)}
      }

    /// Executes the build script
    let run script =

      let (XakeScript (options,rules)) = script
      let logger = CombineLogger (ConsoleLogger options.ConLogLevel) options.CustomLogger

      let logger = match options.FileLog with
        | null | "" -> logger
        | logFileName -> CombineLogger logger (FileLogger logFileName options.FileLogLevel)

      let (throttler, pool) = WorkerPool.create logger options.Threads

      let start = System.DateTime.Now
      let db = Storage.openDb options.ProjectRoot logger
      let ctx = {TaskPool = pool; Throttler = throttler; Options = options; Rules = rules; Logger = logger; Db = db }

      logger.Log Message "Options: %A" options

      let rec unwindAggEx (e:System.Exception) = seq {
        match e with
          | :? System.AggregateException as a -> yield! a.InnerExceptions |> Seq.collect unwindAggEx
          | a -> yield a
        }

      try
        try
          options.Want |> (List.map (makeTarget ctx) >> exec ctx >> Async.RunSynchronously >> ignore)
          logger.Log Message "\n\n\tBuild completed in %A\n" (System.DateTime.Now - start)
        with 
          | exn ->
            let th = if options.FailOnError then raiseError else reportError
            let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) |> Seq.toArray in
            th ctx (exn.Message + "\n" + System.String.Join("\r\n      ", errors)) exn
            logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
      finally
        db.PostAndReply Storage.CloseWait

  /// Script builder.
  type RulesBuilder(options) =

    let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
    let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Want = f(options.Want)}, rules)

    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options, Rules Map.empty)
    member o.Yield(())  = o.Zero()

    member this.Run(script) = Impl.run script
      
    [<CustomOperation("rule")>] member this.Rule(script, rule)                  = updRules script (Impl.addRule rule)
    [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action) = updRules script (Impl.makeFileRule pattern action |> Impl.addRule)
    [<CustomOperation("phony")>] member this.Phony(script, name, action)        = updRules script (Impl.makePhonyRule name action |> Impl.addRule)
    [<CustomOperation("rules")>] member this.Rules(script, rules)               = (rules |> List.map Impl.addRule |> List.fold (>>) id) |> updRules script

    [<CustomOperation("want")>] member this.Want(script, targets)                = updTargets script (function |[] -> targets | _ as x -> x)  // Options override script!
    [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets) = updTargets script (fun _ -> targets)

  /// creates xake build script
  let xake options = new RulesBuilder(options)

  /// key function implementation
  /// Executes and awaits specified artifacts
  let needFileset fileset =
      action {
        let! ctx = getCtx()
        do! fileset |> (toFileList ctx.Options.ProjectRoot >> List.map (fun f -> new Artifact (f.FullName) |> FileTarget)) |> Impl.needTarget
      }

  /// Executes and awaits specified artifacts
  let need targets =
      action {
        let! ctx = getCtx()
        let t' = targets |> (List.map (Impl.makeTarget ctx))
        do!  t' |> Impl.needTarget
      }

  let needTgt = Impl.needTarget

  /// Writes a message to a log
  let writeLog = Impl.writeLog

  /// Creates the rule for specified file pattern.  
  let ( *> ) = Impl.makeFileRule

  /// Creates phony action (check if I can unify the operator name)
  let (=>) = Impl.makePhonyRule

  // Helper method to obtain script options within rule/task implementation
  let getCtxOptions() = action {
    let! (ctx: ExecContext) = getCtx()
    return ctx.Options
  }