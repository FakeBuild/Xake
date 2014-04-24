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

