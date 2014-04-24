namespace Xake

[<AutoOpen>]
module XakeScript =

  type ArtifactMask = FilePattern of string
  type Rule = Rule of ArtifactMask * BuildAction
  type Rules = Rules of Map<ArtifactMask,BuildAction>

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

  /// Main type.
  type XakeScript = XakeScript of XakeOptionsType * Rules

  /// Defaulta options
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

    let setOptions (options:string) :Rules =
      Rules Map.empty // TODO implement

    // locates the rule // TODO replace with XakeScript
    let locateRule (rules:Map<ArtifactMask,BuildAction>) projectRoot (artifact:Artifact) : BuildAction option =
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

(* TODO

  // executes single artifact
  let private execOne artifact =
    match locateRule artifact with
    | Some rule ->
      async {
        let! task = actionPool.PostAndAsyncReply(fun chnl -> Run(artifact, rule, chnl))
        return! task
      }
    | None ->
      if not artifact.Exists then exitWithError 2 (sprintf "Neither rule nor file is found for '%s'" (fullname artifact)) ""
      Async.FromContinuations (fun (cont,_,_) -> cont())

  let exec = Seq.ofList >> Seq.map execOne >> Async.Parallel
  let need x = async {

    throttler.Release() |> ignore
    do! x |> (getFiles >> exec >> Async.Ignore)
    do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
    }

  /// Runs execution of all artifact rules in parallel
  let run targets =
    try
      targets |>
        (List.map (~&) >> exec >> Async.RunSynchronously >> ignore)
    with 
      | :? System.AggregateException as a ->
        let errors = a.InnerExceptions |> Seq.map (fun e -> e.Message) |> Seq.toArray
        exitWithError 255 (a.Message + "\n" + System.String.Join("\r\n      ", errors)) a
      | exn -> exitWithError 255 exn.Message exn


  let rule = async

*)

  /// Creates the rule for specified file pattern.  
  let ( *> ) pattern action =
    Impl.makeFileRule pattern action

  /// Script builder.
  type RulesBuilder(options) =
    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options, Rules Map.empty)

    member this.Yield(()) = XakeScript (options, Rules Map.empty)
    member this.Run(XakeScript (options,rules)) =
      printfn "running"
      printfn "Options: %A" options 
      printfn "Rules: %A" rules

      // TODO run options.Want
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
