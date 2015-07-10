namespace Xake

[<AutoOpen>]
module Action =

  open BuildLog

  module private A =
      let runAction (Action r) = r
      let resultF a = Action (fun (s,_) -> async {return (s,a)})

      let bindF m f = Action (fun (s,r) -> async {
          let! (s',a) = runAction m (s,r) in
          return! runAction (f a) (s',r)
          })

      let resultFromF m = m

      let callF f a = bindF (resultF a) f
      let delayF f = callF f ()
      let bindA ac f = Action (fun (s,r) -> async {
          let! a = ac in return! runAction (f a) (s,r)
          })

      //let doneF = resultF()
      let doneF = Action (fun (s,_) -> async {return (s,())})
      let ignoreF p = bindF p (fun _ -> doneF)
      let combineF f g = bindF f (fun _ -> g)

      let rec whileF guard prog =
        if not (guard()) then 
            doneF
        else 
            bindF prog (fun () -> whileF guard prog) 

      let forF (e: seq<_>) prog =
        let ie = e.GetEnumerator()
        whileF
            (fun () -> ie.MoveNext())
            (delayF(fun () -> prog ie.Current))
(*
      let tryF body handler =
        try
            resultFromF (body())
        with
            e -> handler e

      let tryFinallyF comp body =
        try
            resultFromF (body())
        finally
            comp()

      let usingF (r:'T :> System.IDisposable) f =  
        tryFinallyF (fun () -> r.Dispose()) (callF f r)

      let forF1 (e: seq<_>) prog =
        usingF (e.GetEnumerator()) (fun ie ->
            whileF
                (fun () -> ie.MoveNext())
                (delayF(fun () -> prog ie.Current))
        )
*)
  open A
  let private (>>=) = bindF

  type ActionBuilder() =
    member this.Return(c) = resultF c
    member this.Zero()    = doneF
    member this.Delay(f)  = delayF f

    // binds both monadic and for async computations
    member this.Bind(m, f) = m >>= f
    member this.Bind(m, f) = bindA m f
    member this.Bind((), f) = resultF() >>= f

    member this.Combine(f, g) = combineF f g
    member this.For(seq, f) = forF seq f

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

  /// <summary>
  /// Gets current task result.
  /// </summary>
  let getResult()  = Action (fun (s,_) -> async {return (s,s)})

  /// <summary>
  /// Updates the build result
  /// </summary>
  /// <param name="s'"></param>
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
  let Ignore act = act |> ignoreF
