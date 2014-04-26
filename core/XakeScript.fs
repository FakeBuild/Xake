namespace Xake

[<AutoOpen>]
module XakeScript =
  open System.Threading

  type ArtifactMask = FilePattern of string

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

  type ExecContext = {
    TaskPool: MailboxProcessor<Core.ExecMessage>
    Throttler: SemaphoreSlim
    Options: XakeOptionsType
    Rules: Rules
  }
  and BuAction = Artifact -> Action<ExecContext,unit>
  and Rule = Rule of ArtifactMask * BuAction
  and Rules = Rules of Map<ArtifactMask,BuAction>

  /// Main type.
  type XakeScript = XakeScript of XakeOptionsType * Rules

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
    let makeFileRule pattern action = Rule ((FilePattern pattern), action)

    let addRule (Rule (selector,action)) (Rules rules) :Rules =
      rules |> Map.add selector action |> Rules

    // locates the rule
    let locateRule (Rules rules) projectRoot (artifact:Artifact) : BuAction option =
      let matchRule (FilePattern pattern) b = 
        match Fileset.matches pattern projectRoot artifact.FullName with
          | true ->
            log Verbose "Found pattern '%s' for %s" pattern artifact.Name
            Some (b)
          | false -> None
      rules |> Map.tryPick matchRule

    let locateRuleOrDie r p a =
      match locateRule r p a with
      | Some rule -> rule
      | None -> failwithf "Failed to locate file for '%s'" (fullname a)

  module private ExecScript =

    open Core

    /// creates script execution context
    let createContext (XakeScript (options,rules)) =
      let (throttler, pool) = Core.createActionPool options.Threads
      in
      {
        TaskPool = pool
        Throttler = throttler
        Options = options
        Rules = rules
      }

    // executes single artifact
    let private execOne ctx artifact =
      match Impl.locateRule ctx.Rules ctx.Options.ProjectRoot artifact with
      | Some rule ->
        async {
          let (Action r) = rule artifact
          let! task = ctx.TaskPool.PostAndAsyncReply(fun chnl -> Run(artifact, r ctx, chnl))
          return! task
        }
      | None ->
        if not artifact.Exists then exitWithError 2 (sprintf "Neither rule nor file is found for '%s'" (fullname artifact)) ""
        Async.FromContinuations (fun (cont,_,_) -> cont())

    /// Executes several artifacts in parallel
    let exec ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

    /// Executes and awaits specified artifacts
    let need ctx fileset = async {
        ctx.Throttler.Release() |> ignore
        do! fileset |> (getFiles >> exec ctx >> Async.Ignore)
        do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
      }

    /// Runs execution of all artifact rules in parallel
    let run ctx targets =
      try
        targets |>
          (List.map (~&) >> exec ctx >> Async.RunSynchronously >> ignore)
      with 
        | :? System.AggregateException as a ->
          let errors = a.InnerExceptions |> Seq.map (fun e -> e.Message) |> Seq.toArray
          exitWithError 255 (a.Message + "\n" + System.String.Join("\r\n      ", errors)) a
        | exn -> exitWithError 255 exn.Message exn

  /// Creates the rule for specified file pattern.  
  let ( *> ) pattern action =
    Impl.makeFileRule pattern action

  /// Script builder.
  type RulesBuilder(options) =
    member o.Bind(x,f) = f x
    member o.For(s,f) = for i in s do f i
    member o.Zero() = XakeScript (options, Rules Map.empty)

    member this.Run(script) =

      let (XakeScript (options,rules)) = script
      let ctx = ExecScript.createContext script
      
      let start = System.DateTime.Now
      printfn "running"
      printfn "Options: %A" options 

      // TODO implement run above, no need in multiple exposed functions
      ExecScript.run ctx options.Want

      printfn "\nBuild completed in %A" (System.DateTime.Now - start)
      ()

    [<CustomOperation("rule")>]   member this.Rule(XakeScript (options,rules), rule) = XakeScript (options, Impl.addRule rule rules)
    [<CustomOperation("want")>]
    member this.Want(XakeScript (options,rules), targets) =
      XakeScript (
        {options with
          Want =
            match options.Want with
            | [] -> targets
            | _ as o -> o
            }, rules)

  let xake options = new RulesBuilder(options)

  /// key function implementation
  let need targets = action {
    let! ctx = getCtx
    // printfn "need([%A]) in %A" targets ctx
    do! (ExecScript.need ctx targets)
  }
