module Xake.WorkerPool

type ExecMessage<'target,'result> =
  | Run of string * 'target list * Async<'result> * AsyncReplyChannel<Async<'result>>

open System.Threading
open System.Threading.Tasks

// execution context
let create (logger:ILogger) maxThreads =
    // controls how many threads are running in parallel
    let throttler = new SemaphoreSlim (maxThreads)
    let log = logger.Log

    throttler, MailboxProcessor.Start(fun mbox ->
        let rec loop(map) = async {
            match! mbox.Receive() with
            | Run(_, [], _, _) ->
                log Error "Empty target list"
                return! loop map
            | Run(title, (artifact::_ as targets), action, chnl) ->
                match map |> Map.tryFind artifact with
                | Some (task:Task<'a>) ->
                    log Never "Task found for '%s'. Status %A" title task.Status
                    chnl.Reply <| Async.AwaitTask task
                    return! loop map
  
                | None ->
                    do log Info "Task queued '%s'" title
                    do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
                  
                    let task = Async.StartAsTask <| async {
                        try
                            let! buildResult = action
                            do log Info "Task done '%s'" title
                            return buildResult
                        finally
                            throttler.Release() |> ignore
                    }

                    chnl.Reply <| Async.AwaitTask task
                    let map' = List.fold (fun m t -> m |> Map.add t task) map targets
                    return! loop map'
        }
        loop(Map.empty) )
