namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading
  open System.Threading.Tasks

  open Xake

  // task pool
  let MaxThreads = 2

  // execution context
  type ExecMessage =
    | Run of Artifact * BuildAction * AsyncReplyChannel<Async<unit>>

  // controls how many threads are running in parallel
  let private throttler = new SemaphoreSlim (MaxThreads)

  let actionPool = MailboxProcessor.Start(fun mbox ->
    let rec loop(map) = async {
      let! msg = mbox.Receive()
      match msg with
      | Run(artifact, BuildAction action, chnl) ->
        match map |> Map.tryFind artifact.FullName with
        | Some task ->
          log Verbose "Task found for '%s'. Waiting for completion" artifact.Name
          chnl.Reply(Async.AwaitTask task)
          return! loop(map)

        | None ->
          do log Verbose "Starting new task for '%s'" artifact.Name
          do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
          
          let task = Async.StartAsTask (async {
            try
              do! action artifact
            finally
              throttler.Release() |> ignore
          })
          chnl.Reply(Async.AwaitTask task)
          return! loop(map |> Map.add artifact.FullName task)
    }
    loop(Map.empty) )

  let exitWithError errorCode error details =
    log Error "Error '%s'. See build.log for details" error
    log Verbose "Error details are:\n%A\n\n" details
    //exit errorCode

  // TODO how does it work?
  actionPool.Error.Add(fun e -> exitWithError 1 e.Message e)

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
