namespace Xake

[<AutoOpen>]
module Action =

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

      let doneF = Action (fun (s,_) -> async {return (s,())})
      let ignoreF p = bindF p (fun _ -> doneF)
      let combineF f g = bindF f (fun _ -> g)

      let rec whileF guard prog =
        if not (guard()) then 
            doneF
        else 
            (fun () -> whileF guard prog) |> bindF prog

      let tryF body handler =
        try
            body() |> resultFromF
        with
            e -> handler e

      let tryFinallyF body comp =
        try
            body() |> resultFromF
        finally
            comp()

      let usingF (r:'T :> System.IDisposable) body =
        let body' = fun () -> body r
        tryFinallyF body' (fun () -> r.Dispose())

      let forF (e: seq<_>) prog =
        usingF (e.GetEnumerator()) (fun ie ->
            whileF
                (fun () -> ie.MoveNext())
                ((fun () -> prog ie.Current) |> delayF)
        )


      // temporary defined overloads suitable for

//      let tryFinallyF2 (body:Action<'a,unit>) (comp: unit -> Action<'a,unit>) :Action<'a,unit> =
//        try
//            printfn "TryWith Body"
//            let m = delayF (fun() -> body)
//            printfn "TryWith Body/return"
//            let r = resultFromF m
//            //resultFromF body
//            printfn "TryWith Body/return2"
//            r
//        finally
//            printfn "TryWith Finally"
//            //delayF comp |> ignore
//            delayF comp |> resultFromF |> ignore
//
//      let tryF2 body handler =
//        try
//            resultFromF body
//        with
//            e -> handler e

  open A
  let private (>>=) = bindF

  type ActionBuilder() =
    member this.Return(c) = resultF c
    member this.Zero()    = doneF
    member this.Delay(f)  = delayF f

    // binds both monadic and for async computations
    member this.Bind(m, f) = bindF m f
    member this.Bind(m, f) = bindA m f
    member this.Bind((), f) = resultF () >>= f

    member this.Combine(f, g) = combineF f g
    member this.While(guard, body) = whileF guard body
    member this.For(seq, f) = forF seq f

//    member this.TryWith(body, handler) = tryF (fun () -> body) handler
//    member this.TryFinally(body, compensation) = tryFinallyF (fun () -> body) compensation
//    member this.Using(disposable:#System.IDisposable, body) = usingF disposable body

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

  /// <summary>
  /// Consumes the task output and in case condition is met raises the error.
  /// </summary>
  /// <param name="cond"></param>
  /// <param name="act"></param>
  [<System.Obsolete("Proposed function. Name and signature might be changed")>]
  let FailWhen cond err act = Action (fun (r,c) -> 
    async {
        let! (r',c') = A.runAction act (r,c)
        if cond c' then failwith err
        return (r',())
    })

  /// <summary>
  /// Supplemental for FailIf to verify errorlevel set by system command.
  /// </summary>
  let Not0 = (<>) 0

  /// <summary>
  /// Error handler verifying result of system command.
  /// </summary>
  /// <param name="act"></param>
  let CheckErrorLevel act = act |> FailWhen Not0 "system command returned a non-zero result"

  /// <summary>
  /// Wraps action so that exceptions occured while executing action are ignored.
  /// </summary>
  /// <param name="act"></param>
  let WhenError h (act:Action<'a,unit>) = 
      Action (fun (r,a) -> async {
        try
            let! (r',_) = A.runAction act (r,a)
            return (r',())
        with
            e ->
                do h e 
                return (r,())
      })
