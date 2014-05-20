namespace Xake

[<AutoOpen>]
module Action =

  open BuildLog

  // expression type
  type Action<'a,'b> = Action of (BuildResult * 'a -> Async<BuildResult * 'b>)

  // reset context for nested action
  let private runAction (Action r) = r
  let private returnF a = Action (fun (s,_) -> async {return (s,a)})

  // bind action in expression like do! action {...}
  let private (>>=) m f = Action (fun (s,r) -> async {
      let! (s',a) = runAction m (s,r) in
      return! runAction (f a) (s',r)
      })

  let private bindAsync ac f = Action (fun (s,r) -> async {
      let! a = ac in return! runAction (f a) (s,r)
      })
  
  type ActionBuilder() =
    member this.Return(c) = returnF c
    member this.Zero()    = returnF ()
    member this.Delay(f)  = returnF() >>= f

    // binds both monadic and for async computations
    member this.Bind(m, f) = m >>= f
    member this.Bind(m, f) = bindAsync m f
    member this.Bind((), f) = this.Zero() >>= f

    member this.Combine(r1, r2) = r1 >>= fun _ -> r2
    member this.For(seq:seq<_>, f)  = Action (fun (s,x) -> async {
      for i in seq do
        runAction (f i) x |> ignore // TODO collect DepStatus
      return s,()
      })

  /// Builder fot xake actions.
  let action = ActionBuilder()

  // other (public) functions for Action

  /// Gets action context
  let getCtx()     = Action (fun (r,c) -> async {return (r,c)})
  let getResult()  = Action (fun (s,_) -> async {return (s,s)})
  let setResult s' = Action (fun (_,_) -> async {return (s',())})

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
          | Some (task:Task<unit>) ->
 
            chnl.Reply(task.IsCompleted |> function
              |true -> async{()}
              | _ ->
                log Debug "Task found for '%s'. Waiting for completion" (getShortname artifact)
                Async.AwaitTask task)
 
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

