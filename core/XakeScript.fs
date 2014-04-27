namespace Xake

[<AutoOpen>]
module XakeScript =
  open System.Threading

  type XakeOptionsType = {
    /// Defines project root folder
    ProjectRoot : string
    /// Maximum number of threads to run the rules
    Threads: int

    /// Log file and verbosity level
    FileLog: string
    FileLogLevel: Level

    /// Console output verbosity level. Default is Warn
    ConLogLevel: Level
    /// Overrides "want", i.e. target list 
    Want: string list
  }

  type ArtifactMask = FilePattern of string
  type BuildAction<'ctx> = Artifact -> Action<'ctx,unit>
  type Rule<'ctx> = Rule of ArtifactMask * BuildAction<'ctx>
  type Rules<'ctx> = Rules of Map<ArtifactMask,BuildAction<'ctx>>

  type ExecContext = {
    TaskPool: MailboxProcessor<WorkerPool.ExecMessage>
    Throttler: SemaphoreSlim
    Options: XakeOptionsType
    Rules: Rules<ExecContext>
  }

  /// Main type.
  type XakeScript = XakeScript of XakeOptionsType * Rules<ExecContext>

  /// Default options
  let XakeOptions = {
    ProjectRoot = System.IO.Directory.GetCurrentDirectory()
    Threads = 4
    ConLogLevel = Level.Warning

    FileLog = ""
    FileLogLevel = Level.Error
    Want = []
    }

  module private Impl =
    open WorkerPool

    let makeFileRule pattern action = Rule ((FilePattern pattern), action)

    let addRule (Rule (selector,action)) (Rules rules) :Rules<_> =
      rules |> Map.add selector action |> Rules

    // locates the rule
    let private locateRule (Rules rules) projectRoot (artifact:Artifact) : BuildAction<_> option =
      let matchRule (FilePattern pattern) b = 
        match Fileset.matches pattern projectRoot (getFullname artifact) with
          | true ->
            Logging.log Verbose "Found pattern '%s' for %s" pattern (getShortname artifact)
            Some (b)
          | false -> None
      rules |> Map.tryPick matchRule

    // executes single artifact
    let private execOne ctx artifact =
      match locateRule ctx.Rules ctx.Options.ProjectRoot artifact with
      | Some rule ->
        async {
          let (Action r) = rule artifact
          let! task = ctx.TaskPool.PostAndAsyncReply(fun chnl -> Run(artifact, r ctx, chnl))
          return! task
        }
      | None ->
        if not <| exists artifact then exitWithError 2 (sprintf "Neither rule nor file is found for '%s'" (getFullname artifact)) ""
        Async.FromContinuations (fun (cont,_,_) -> cont())

    /// Executes several artifacts in parallel
    let private exec ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

    /// Executes and awaits specified artifacts
    let need fileset = action {
        let! ctx = getCtx
        ctx.Throttler.Release() |> ignore
        do! fileset |> (toFileList ctx.Options.ProjectRoot >> List.map FileArtifact >> exec ctx >> Async.Ignore)
        do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
      }

    /// Executes the build script
    let run script =

      let (XakeScript (options,rules)) = script
      let (throttler, pool) = WorkerPool.create options.Threads
      let ctx = {TaskPool = pool; Throttler = throttler; Options = options; Rules = rules}

      try
        options.Want |> (List.map toArtifact >> exec ctx >> Async.RunSynchronously >> ignore)
      with 
        | :? System.AggregateException as a ->
          let errors = a.InnerExceptions |> Seq.map (fun e -> e.Message) |> Seq.toArray
          exitWithError 255 (a.Message + "\n" + System.String.Join("\r\n      ", errors)) a
        | exn -> exitWithError 255 exn.Message exn

  /// Script builder.
  type RulesBuilder(options) =

    let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
    let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Want = f(options.Want)}, rules)

    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options, Rules Map.empty)
    member o.Yield(())  = o.Zero()

    member this.Run(script) =

      let start = System.DateTime.Now
      printfn "Options: %A" options
      Impl.run script
      printfn "\nBuild completed in %A" (System.DateTime.Now - start)
      ()

    [<CustomOperation("rule")>] member this.Rule(script, rule)                  = updRules script (Impl.addRule rule)
    [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action) = updRules script (Impl.makeFileRule pattern action |> Impl.addRule)
    [<CustomOperation("rules")>] member this.Rules(script, rules)     = (rules |> List.map Impl.addRule |> List.fold (>>) id) |> updRules script

    [<CustomOperation("want")>] member this.Want(script, targets)                = updTargets script ((@) targets)
    [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets) = updTargets script (fun _ -> targets)

  /// creates xake build script
  let xake options = new RulesBuilder(options)

  /// key function implementation
  let need = Impl.need

  /// Creates the rule for specified file pattern.  
  let ( *> ) pattern action =
    Impl.makeFileRule pattern action

  // Helper method to obtain script options within rule/task implementation
  let getCtxOptions = action {
    let! (ctx: ExecContext) = getCtx
    return ctx.Options
  }