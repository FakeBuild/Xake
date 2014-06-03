namespace Xake

[<AutoOpen>]
module XakeScript =

  open BuildLog
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


  type Rule<'ctx> = 
      | FileRule of string * (Artifact -> Action<'ctx,unit>)
      | PhonyRule of string * Action<'ctx,unit>
      | FileConditionRule of (string -> bool) * (Artifact -> Action<'ctx,unit>)
  type Rules<'ctx> = Rules of Rule<'ctx> list

  type ExecStatus = | Succeed | Skipped | JustFile

  type ExecContext = {
    TaskPool: Agent<WorkerPool.ExecMessage<ExecStatus>>
    Db: Agent<Storage.DatabaseApi>
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
    open BuildLog
    open Storage

    let TimeCompareToleranceMs = 100.0

    /// Writes the message with formatting to a log
    let writeLog (level:Logging.Level) fmt  =
      let write s = action {
        let! (ctx:ExecContext) = getCtx()
        return ctx.Logger.Log level "%s" s
      }
      Printf.kprintf write fmt

    let addRule rule (Rules rules) :Rules<_> =  Rules (rule :: rules)

    // locates the rule
    let private locateRule (Rules rules) projectRoot target =
      let matchRule rule = 
        match rule, target with
          |FileConditionRule (f,_), FileTarget file when (f file.FullName) = true ->
              // writeLog Verbose "Found phony pattern '%s'" name
              Some (rule)
          |FileRule (pattern,_), FileTarget file when Fileset.matches pattern projectRoot file.FullName ->
              // writeLog Verbose "Found pattern '%s' for %s" pattern (getShortname target)
              Some (rule)
          |PhonyRule (name,_), PhonyAction phony when phony = name ->
              // writeLog Verbose "Found phony pattern '%s'" name
              Some (rule)
          | _ -> None
        
      rules |> List.tryPick matchRule

    let private reportError ctx error details =
      do ctx.Logger.Log Error "Error '%s'. See build.log for details" error
      do ctx.Logger.Log Verbose "Error details are:\n%A\n\n" details

    let private raiseError ctx error details =
      do reportError ctx error details
      raise (XakeException(sprintf "Script failed (error code: %A)\n%A" error details))

    /// Gets true if rebuild is required
    let rec needRebuild ctx (tgt:Target) result =

      // check simple rules (all but ArtifactDep) synchronously
      // request "dependencies"
      let isOutdated = function
        | File (a:Artifact, wrtime) ->
            not(a.Exists && abs((a.LastWriteTime - wrtime).TotalMilliseconds) < TimeCompareToleranceMs), sprintf "removed or changed file '%s'" a.Name

        | ArtifactDep (FileTarget file) ->
            not file.Exists, sprintf "target doesn't exist '%s'" file.Name

        | ArtifactDep _ -> false, ""
  
        | EnvVar (name,value) ->
            let newValue = System.Environment.GetEnvironmentVariable(name) in
            value <> newValue, sprintf "Environment variable %s was changed to '%s'" name value

        | Var (name,value) -> false, "Var not implemented"
        | AlwaysRerun -> true, "alwaysRerun rule"
        | GetFiles (fileset,files) ->
        
          let newfiles = fileset |> toFileList ctx.Options.ProjectRoot
          let diff = compareFileList files newfiles

          if List.isEmpty diff then
            false, ""
          else
            true, sprintf "File list is changed for changeset %A: %A" fileset diff

      match result with
        | Some {BuildResult.Depends = []} ->
            true, "No dependencies", []

        | Some {BuildResult.Result = FileTarget file} when not file.Exists ->
            true, "target not found", []

        | Some result ->
            let artifactDeps, immediateDeps = result.Depends |> List.partition (function |ArtifactDep (FileTarget file) when file.Exists -> true | _ -> false)

            match immediateDeps |> List.tryFind (isOutdated >> fst) with
            | Some d ->
              let _,reason = isOutdated d in
              true, reason, []
            | _ ->
              let targets = artifactDeps |> List.map (function |ArtifactDep dep -> dep) in
              false, "", targets

        | _ -> true, "reason unknown (new file&)", []
      |> function
        | true, reason, _ ->
            do ctx.Logger.Log Info "Rebuild %A: %s" (getShortname tgt) reason
            async {return true}
        | _, _, (deps: Target list) ->
            async {
              let! status = execNeed ctx deps
              return fst status = ExecStatus.Succeed
            }

    // executes single artifact
    and private execOne ctx target =

      let run action chnl =
        Run(target,
          async {
            let! lastBuild = (fun ch -> GetResult(target, ch)) |> ctx.Db.PostAndAsyncReply

            let! doRebuild = needRebuild ctx target lastBuild

            if doRebuild then
              do ctx.Logger.Log Command "Started %s" (getShortname target)

              let! (result,_) = action (BuildLog.makeResult target,ctx)
              Store result |> ctx.Db.Post

              do ctx.Logger.Log Command "Completed %s" (getShortname target)
              return ExecStatus.Succeed
            else
              do ctx.Logger.Log Command "Skipped %s (up to date)" (getShortname target)
              return ExecStatus.Skipped
          }, chnl)


      // result expression is...
      target
      |> locateRule ctx.Rules ctx.Options.ProjectRoot
      |> function
          | Some (FileRule (_, action))
          | Some (FileConditionRule (_, action)) ->
            let (FileTarget artifact) = target in
            let (Action r) = action artifact in
            Some r
          | Some (PhonyRule (_, Action r)) -> Some r
          | _ -> None
      |> function
        | Some action ->
          async {
            let! waitTask = run action |> ctx.TaskPool.PostAndAsyncReply
            let! status = waitTask
            return status, Dependency.ArtifactDep target
          }
        | None ->
          // should always fail for phony
          let (FileTarget file) = target
          match file.Exists with
          | true -> async {return ExecStatus.JustFile, Dependency.File (file,file.LastWriteTime)}
          | false -> raiseError ctx (sprintf "Neither rule nor file is found for '%s'" (getFullname target)) ""

    /// Executes several artifacts in parallel
    and private execMany ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

    /// Gets the status of dependency artifacts (obtained from 'need' calls)
    and execNeed ctx targets : Async<ExecStatus * Dependency list> =
      async {
        ctx.Throttler.Release() |> ignore
        let! statuses = targets |> execMany ctx
        do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore

        let dependencies = statuses |> Array.map snd |> List.ofArray in
        let status = match statuses |> Array.exists (fst >> (=) ExecStatus.Succeed) with
                      | true -> ExecStatus.Succeed
                      | _ -> ExecStatus.Skipped

        return status,dependencies
      }

    // phony actions are detected by their name so if there's "clean" phony and file "clean" in `need` list if will choose first
    let makeTarget ctx name =
      let (Rules rr) = ctx.Rules
      if rr |> List.exists (function |PhonyRule (n,_) when n = name -> true | _ -> false) then
        PhonyAction name
      else
        FileTarget (Artifact (ctx.Options.ProjectRoot </> name))    

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
          options.Want |> (List.map (makeTarget ctx) >> execMany ctx >> Async.RunSynchronously >> ignore)
          logger.Log Message "\n\n\tBuild completed in %A\n" (System.DateTime.Now - start)
        with 
          | exn ->
            let th = if options.FailOnError then raiseError else reportError
            let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) |> Seq.toArray in
            th ctx (exn.Message + "\n" + System.String.Join("\r\n      ", errors)) exn
            logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
      finally
        db.PostAndReply Storage.CloseWait

  /// Creates the rule for specified file pattern.  
  let ( *> ) pattern fnRule = FileRule (pattern, fnRule)
  let ( *?> ) fn fnRule = FileConditionRule (fn, fnRule)

  /// Creates phony action (check if I can unify the operator name)
  let (=>) name fnRule = PhonyRule (name,fnRule)

  /// Script builder.
  type RulesBuilder(options) =

    let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
    let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Want = f(options.Want)}, rules)

    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options, Rules [])
    member o.Yield(())  = o.Zero()

    member this.Run(script) = Impl.run script
      
    [<CustomOperation("rule")>] member this.Rule(script, rule)                  = updRules script (Impl.addRule rule)
    [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action) = updRules script (pattern *> action |> Impl.addRule)
    [<CustomOperation("phony")>] member this.Phony(script, name, action)        = updRules script (name => action |> Impl.addRule)
    [<CustomOperation("rules")>] member this.Rules(script, rules)               = (rules |> List.map Impl.addRule |> List.fold (>>) id) |> updRules script

    [<CustomOperation("want")>] member this.Want(script, targets)                = updTargets script (function |[] -> targets | _ as x -> x)  // Options override script!
    [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets) = updTargets script (fun _ -> targets)

  /// creates xake build script
  let xake options =
    new RulesBuilder(options)

  /// Create xake build script using command-line arguments to define script options
  let xakeArgs args options =
    let _::targets = Array.toList args
    // this is very basic implementation which only recognizes target names
    new RulesBuilder({options with Want = targets})

  /// Gets the script options.
  let getCtxOptions() = action {
    let! (ctx: ExecContext) = getCtx()
    return ctx.Options
  }

  /// key functions implementation

  let private needImpl targets =
      action {
        let! ctx = getCtx()
        let! _,deps = targets |> Impl.execNeed ctx

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ deps}
      }

  /// Executes and awaits specified artifacts
  let need targets =
      action {
        let! ctx = getCtx()
        let t' = targets |> (List.map (Impl.makeTarget ctx))

        do! needImpl t'
      }

  let needFiles (Filelist files) =
      action {
        let! ctx = getCtx()
        let targets = files |> List.map (fun f -> new Artifact (f.FullName) |> FileTarget)

        do! needImpl targets
     }

  /// Instructs Xake to rebuild the target evem if dependencies are not changed
  let alwaysRerun () = action {
    let! ctx = getCtx()
    let! result = getResult()
    do! setResult {result with Depends = Dependency.AlwaysRerun :: result.Depends}
  }

  /// Gets the environment variable
  let getEnv variableName = action {
    let value = System.Environment.GetEnvironmentVariable(variableName)
    let! ctx = getCtx()
    
    // record the dependency
    let! result = getResult()
    do! setResult {result with Depends = Dependency.EnvVar (variableName,value) :: result.Depends}

    return value
  }

  /// Executes and awaits specified artifacts
  let getFiles fileset =
      action {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ [Dependency.GetFiles (fileset,files)]}

        return files
     }

  /// Writes a message to a log
  let writeLog = Impl.writeLog

 