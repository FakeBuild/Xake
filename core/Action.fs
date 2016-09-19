namespace Xake

module Action =

  module internal A =
      let runAction (Action r) = r
      let returnF a = Action (fun (s,_) -> async {return (s,a)})

      let bindF m f = Action (fun (s, a) -> async {
          let! (s', b) = runAction m (s, a) in
          return! runAction (f b) (s', a)
          })
      let bindA m f = Action (fun (s, r) -> async {
          let! a = m in
          return! runAction (f a) (s, r)
          })
      let resultFromF m = m

      let callF f a = bindF (returnF a) f
      let delayF f = callF f ()

      let doneF = Action (fun (s,_) -> async {return (s,())})

      let ignoreF p = bindF p (fun _ -> doneF)
      let combineF f g = bindF f (fun _ -> g)

      let rec whileF guard prog =
        if not (guard()) then 
            doneF
        else 
            (fun () -> whileF guard prog) |> bindF prog

      let tryWithF body h = // (body:Action<'a,'b>) -> (h: Action<'exc,'b>) -> Action<'a,'b> =
          fun (r, a) -> async {
              try
                  return! runAction body (r, a)
              with e ->
                  return! runAction h (r, e)
          } |> Action

      let tryFinallyF body comp = // (body:Action<'a,'b>) -> (comp: unit -> unit) -> Action<'a,'b> =
          fun (r, a) -> async {
              try
                  return! runAction body (r,a)
              finally
                  do comp()
          } |> Action

      let usingF (r:'T :> System.IDisposable) body =
          tryFinallyF body (fun () -> r.Dispose())

      let forF (e: seq<_>) prog =
        let enumerator = e.GetEnumerator()
        usingF enumerator (
            whileF
                (fun () -> enumerator.MoveNext())
                ((fun () -> prog enumerator.Current) |> delayF)
        )

  /// <summary>
  /// Ignores action result in case task returns the value but you don't need it.
  /// </summary>
  /// <param name="act"></param>
  let Ignore act = act |> A.ignoreF

//    [<CustomOperation("step")>]
//    member this.Step(m, name) =
//        printfn "STEP %A %A" m name
//        ()

[<AutoOpen>]
module Builder =
    open Action.A
    type ActionBuilder() =
        member this.Return(c) = returnF c
        member this.Zero()    = doneF
        member this.Delay(f)  = delayF f

        // binds both monadic and for async computations
        member this.Bind(m, f) = bindF m f
        member this.Bind(m, f) = bindA m f
        member this.Bind((), f) = bindF (returnF()) f

        member this.Combine(f, g) = combineF f g
        member this.While(guard, body) = whileF guard body
        member this.For(seq, f) = forF seq f
        member this.TryWith(body, handler) = tryWithF body handler
        member this.TryFinally(body, compensation) = tryFinallyF (body) compensation
        member this.Using(disposable:#System.IDisposable, body) = usingF disposable body
    let action = ActionBuilder()
