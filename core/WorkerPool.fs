namespace Xake

module internal WorkerPool =

  open System.IO
  open System.Threading
  open System.Threading.Tasks

  open BuildLog

  // execution context
  type ExecMessage<'r> =
    | Run of Target * Async<'r> * AsyncReplyChannel<Async<'r>>

  let create (logger:ILogger) maxThreads =
    // controls how many threads are running in parallel
    let throttler = new SemaphoreSlim (maxThreads)
    let log = logger.Log

    let mapKey (artifact:Target) = artifact.FullName

    throttler, MailboxProcessor.Start(fun mbox ->
      let rec loop(map) = async {
        let! msg = mbox.Receive()

        match msg with
        | Run(artifact, action, chnl) ->
          let mkey = artifact |> mapKey

          match map |> Map.tryFind mkey with
          | Some (task:Task<'a>) ->
            log Never "Task found for '%s'. Status %A" artifact.ShortName task.Status
            chnl.Reply <| Async.AwaitTask task
            return! loop(map)

          | None ->
            do log Info "Task queued '%s'" artifact.ShortName
            do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
          
            let task = Async.StartAsTask (async {
              try
                let! buildResult = action
                do log Info "Task done '%s'" artifact.ShortName
                return buildResult
              finally
                throttler.Release() |> ignore
            })
            chnl.Reply <| Async.AwaitTask task
            return! loop(map |> Map.add mkey task)
      }
      loop(Map.empty) )
