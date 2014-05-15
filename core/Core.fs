namespace Xake

[<AutoOpen>]
module Action =

  type DepStatus =
    | Valid
    | Rebuild

  // expression type
  type Action<'a,'b> = Action of (DepStatus * 'a -> Async<DepStatus * 'b>)

  // reset context for nested action
  let private runAction (Action r) = r
  let private returnF a = Action (fun (s,_) -> async {return (s,a)})

  // bind action in expression like do! action {...}
  let private bind m f = Action (fun (s,r) -> async {
      let! (s',a) = runAction m (Valid,r) in
      return! runAction (f a) (s',r)
      })

  let private bindAsync ac f = Action (fun (s,r) -> async {
      let! a = ac in return! runAction (f a) (s,r)
      })
  
  type ActionBuilder() =
    member this.Return(c) = returnF c
    member this.Zero()    = returnF ()
    member this.Delay(f)  = bind (returnF ()) f

    // binds both monadic and for async computations
    member this.Bind(m, f) = bind m f
    member this.Bind(m, f) = bindAsync m f
    member this.Bind((), f) = bind (this.Zero()) f

    member this.Combine(r1, r2) = bind r1 (fun _ -> r2)
    member this.For(seq:seq<_>, f)  = Action (fun (s,x) -> async {
      for i in seq do
        runAction (f i) x |> ignore // TODO collect DepStatus
      return Valid,()
      })

    // custom operations
//    member o.Yield(())  = o.Zero()

//    [<CustomOperation("ifneed")>]
//    member this.IfNeed(a) =
//      // TODO
//      bind (returnF ()) a

  /// Builder fot xake actions.
  let action = ActionBuilder()

  // other (public) functions for Action

  /// Gets action context
  let getCtx = Action (fun p -> async {return p})
  let getStatus = Action (fun (s,_) -> async {return (s,s)})

module WorkerPool =

  open System.IO
  open System.Threading
  open System.Threading.Tasks

  open Xake

  // execution context
  type ExecMessage =
    | Run of Target * Async<unit> * AsyncReplyChannel<Async<unit>>

  let create (logger:ILogger) maxThreads =
    // controls how many threads are running in parallel
    let throttler = new SemaphoreSlim (maxThreads)
    let log = logger.Log

    throttler, MailboxProcessor.Start(fun mbox ->
      let rec loop(map) = async {
        let! msg = mbox.Receive()
        match msg with
        | Run(artifact, action, chnl) ->
          let fullname = getFullname artifact
          match map |> Map.tryFind fullname with
          | Some task ->
            log Debug "Task found for '%s'. Waiting for completion" (getShortname artifact)
            chnl.Reply(Async.AwaitTask task)
            return! loop(map)

          | None ->
            do log Command "Queued '%s'" (getShortname artifact)
            do! throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore
          
            let task = Async.StartAsTask (async {
              try
                do! action
                do log Command "Done '%s'" (getShortname artifact)
              finally
                throttler.Release() |> ignore
            })
            chnl.Reply(Async.AwaitTask task)
            return! loop(map |> Map.add fullname task)
      }
      loop(Map.empty) )


  // TODO how does it work?
  // actionPool.Error.Add(fun e -> exitWithError 1 e.Message e)

