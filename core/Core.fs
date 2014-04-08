namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading.Tasks

  open Xake

  // task pool
  let MaxThreads = 5

  type TaskPoolMessage =
    | RunTask of Async<unit> * AsyncReplyChannel<Task<unit>>
    | TaskComplete
    | TaskSuspend of bool

    // TODO resume with awaiting the pool is free

  let taskPool = MailboxProcessor.Start(fun mbox ->
    
    let wrapTask a = async {
      try
        do! a
      finally
        mbox.Post TaskComplete
    }

    let rec loop x p =
      //log Info "loop %A [%A]" x (List.length p)
      if x >= MaxThreads || not (List.isEmpty p) then wait x p else run x p

    and run taskCount pool = (async {
      let! msg = mbox.Receive()
      match msg with
      | RunTask (task,chnl) ->
        chnl.Reply(Async.StartAsTask (wrapTask task))
        return! loop (taskCount + 1) pool
      | TaskComplete ->
        return! loop (taskCount - 1) pool
      | TaskSuspend true ->
        return! loop (taskCount - 1) pool
      | TaskSuspend false ->
        return! loop (taskCount + 1) pool
    })
    and wait taskCount pool = (async {
      let! msg = mbox.Receive()
      match msg with
      | RunTask (task,chnl) ->
        let t = new Task<unit> (fun() -> Async.RunSynchronously (wrapTask task))
        chnl.Reply(t)
        return! loop taskCount (pool @ [t]) // TODO use queue
      | TaskComplete
      | TaskSuspend true ->
        match pool with
        |t::ts -> 
          t.Start()
          return! loop taskCount ts
        | [] ->
          return! loop (taskCount-1) pool
      | TaskSuspend false ->
        return! loop (taskCount + 1) pool
    })
    loop 0 [] )

  // execution context
  type ExecMessage =
    | Run of Artifact * BuildAction * AsyncReplyChannel<Async<unit>>

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
          log Verbose "Starting new task for '%s'" artifact.Name
          let! task = taskPool.PostAndAsyncReply(fun chnl -> RunTask(action artifact, chnl))
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
    do taskPool.Post (TaskSuspend true)
    do! x |> (getFiles >> exec >> Async.Ignore)
    do taskPool.Post (TaskSuspend false)
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
