namespace Xake

[<AutoOpen>]
module Action =

  open BuildLog

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

//    [<CustomOperation("step")>]
//    member this.Step(m, name) =
//        printfn "STEP %A %A" m name
//        ()

  /// Builder for xake actions.
  let action = ActionBuilder()

  // other (public) functions for Action

  /// <summary>
  /// Gets action context.
  /// </summary>
  let getCtx()     = Action (fun (r,c) -> async {return (r,c)})

  let getResult()  = Action (fun (s,_) -> async {return (s,s)})
  let setResult s' = Action (fun (_,_) -> async {return (s',())})

  /// <summary>
  /// Finalizes current build step and starts a new one
  /// </summary>
  /// <param name="name">New step name</param>
  let newstep name =
    Action (fun (r,_) ->
        async {
            let r' = Step.updateTotalDuration r
            let r'' = {r' with Steps = (Step.start name) :: r'.Steps}
            return (r'',())
        })
  
  /// <summary>
  /// Ignores action result in case task returns the value but you don't need it.
  /// </summary>
  /// <param name="act"></param>
  let Ignore act = act >>= (fun _ -> returnF ())
