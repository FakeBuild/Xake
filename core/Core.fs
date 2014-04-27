namespace Xake

[<AutoOpen>]
module Action =
  // expression type
  type Action<'a,'b> = Action of ('a -> Async<'b>)

  let private runAction (Action r) ctx = r ctx
  let private returnF a = Action (fun _ -> async {return a})
  let private bind m f = Action (fun r -> async {
      let! a = runAction m r in return! runAction (f a) r
      })

  let private bindA ac f = Action (fun r -> async {
      let! a = ac in return! runAction (f a) r
      })
  
  type ActionBuilder() =
    member this.Return(c) = returnF c
    member this.Zero()    = returnF ()
    member this.Delay(f)  = bind (returnF ()) f

    // binds both monadic and for async computations
    member this.Bind(m, f) = bind m f
    member this.Bind(m, f) = bindA m f

    member this.Combine(r1, r2) = bind r1 (fun () -> r2)
    member this.For(s:seq<_>, f)  = Action (fun x -> async {
      for i in s do runAction (f i) x |> ignore
      })

  // other (public) functions for Action

  /// Gets action context
  let getCtx = Action (fun ctx -> async {return ctx})

  let action = ActionBuilder()

module WorkerPool =

  open System.IO
  open System.Threading
  open System.Threading.Tasks

  open Xake

  // execution context
  type ExecMessage =
    | Run of Artifact * Async<unit> * AsyncReplyChannel<Async<unit>>

  let create maxThreads =
    // controls how many threads are running in parallel
    let throttler = new SemaphoreSlim (maxThreads)

    throttler, MailboxProcessor.Start(fun mbox ->
      let rec loop(map) = async {
        let! msg = mbox.Receive()
        match msg with
        | Run(artifact, action, chnl) ->
          let fullname = getFullname artifact
          match map |> Map.tryFind fullname with
          | Some task ->
            log Verbose "Task found for '%s'. Waiting for completion" (getShortname artifact)
            chnl.Reply(Async.AwaitTask task)
            return! loop(map)

          | None ->
            do log Verbose "Starting new task for '%s'" (getShortname artifact)
            do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
          
            let task = Async.StartAsTask (async {
              try
                do! action
              finally
                throttler.Release() |> ignore
            })
            chnl.Reply(Async.AwaitTask task)
            return! loop(map |> Map.add fullname task)
      }
      loop(Map.empty) )

  let exitWithError errorCode error details =
    log Error "Error '%s'. See build.log for details" error
    log Verbose "Error details are:\n%A\n\n" details
    //exit errorCode

  // TODO how does it work?
  // actionPool.Error.Add(fun e -> exitWithError 1 e.Message e)

