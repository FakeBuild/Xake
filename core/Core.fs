namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading.Tasks

  open Xake

  // execution context
  type ExecMessage =
    | Run of Artifact * BuildAction * AsyncReplyChannel<Task<FileInfo>>
    | Reset
    | GetTask of FileInfo * AsyncReplyChannel<Task<FileInfo> option>

  let execstate = MailboxProcessor.Start(fun mbox ->
    let rec loop(map) = async {
      let! msg = mbox.Receive()
      match msg with
      | Reset ->        
        // TODO cancel pending tasks
        // map |> Map.iter (fun file task -> ...)
        return! loop(Map.empty)

      | Run(artifact, BuildAction action, chnl) ->
        
        let fullname = fullname artifact

        match map |> Map.tryFind fullname with
        | Some task ->
          log Verbose "Task found for '%s'. Waiting for completion" artifact.Name
          chnl.Reply(task)
          return! loop(map)

        | None ->
          let task = Async.StartAsTask (async {
            do! action artifact
            return artifact
          })
          log Verbose "Starting new task for '%s'" artifact.Name
          chnl.Reply(task)
          return! loop(map |> Map.add fullname task)

      | GetTask(file,chnl) ->        
        chnl.Reply (map |> Map.tryFind file.FullName)
        return! loop(map)
    }
    loop(Map.empty) )

  let exitWithError errorCode error details =
    log Error "Error '%s'. See build.log for details" error
    log Verbose "Error details are:\n%A\n\n" details
    //exit errorCode

  // TODO how does it work?
  execstate.Error.Add(fun e -> exitWithError 1 e.Message e)

  // TODO make a parameter
  let projectRoot = Directory.GetCurrentDirectory()
  let mutable private rules:Map<FilePattern,BuildAction> = Map.empty

  // locates the rule
  let internal locateRule (artifact:Artifact) : BuildAction option =
    let matchRule pattern b = 
      match Fileset.matches pattern projectRoot artifact.FullName with
        | true ->
          log Verbose "Found pattern '%s' for %s" pattern artifact.Name
          Some (b)
        | false -> None

    rules |> Map.tryPick matchRule

  let locateRuleOrDie a =
    match locateRule a with
    | Some rule -> rule
    | None -> failwithf "Failed to locate file for '%s'" (fullname a)

  // creates new artifact rule
  let ( *> ) selector buildfile : unit =
    let action = BuildAction buildfile
    rules <- rules |> Map.add selector action

  // executes single artifact
  let private execOne artifact =
    match locateRule artifact with
    | Some rule ->
      async {
        let! task = execstate.PostAndAsyncReply(fun chnl -> Run(artifact, rule, chnl))
        return! Async.AwaitTask task
      }
    | None ->
      if not artifact.Exists then exitWithError 2 (sprintf "Neither rule nor file is found for '%s'" (fullname artifact)) ""
      Async.FromContinuations (fun (cont,_,_) -> cont(artifact))

  let exec = Seq.ofList >> Seq.map execOne >> Async.Parallel
  let need artifacts = artifacts |> exec |> Async.Ignore

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
