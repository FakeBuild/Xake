module Research.Action

open System

// expression type
type Action<'a,'b> = Action of ('a -> Async<'b>)

let runAction (Action r) ctx = r ctx
let returnF a = Action (fun _ -> async {return a})
let bind m f = Action (fun r -> async {
    let! a = runAction m r in return! runAction (f a) r
    })

let bindA ac f = Action (fun r -> async {
    let! a = ac in return! runAction (f a) r
    })
  
type ActionBuilder<'x>() =
  //see for Reader monad https://github.com/fsprojects/fsharpx/blob/master/src/FSharpx.Core/ComputationExpressions/Monad.fs  
  member this.Return(c) = returnF c
  member this.Zero()    = returnF ()
  member this.Delay(f)  = bind (returnF ()) f

  // monadic bind
  member this.Bind(m, f) = bind m f
  member this.Bind(m, f) = bindA m f
//  member this.For(Action s, f)  = Action (fun x -> async {let! a = s x in return! runAction (f a) x})
//
//  [<CustomOperation("need")>]
//  member this.Need(Action a,  targets: string list) =
//    Action (fun x ->
//      let r = a x
//      printfn "need(%A, [%A])" a targets
//      r)
////    printfn "need(%A, [%A])" a targets
////    Action a
//
//   //member this.Combine(r1, r2) = this.Bind(r1, fun _ -> r2)
//   member this.Yield(_) =
//    //printfn "yield"
//    this.Zero()
let action = ActionBuilder<string>()

/////////////////////////////////////////////////////////////

let getCtx =
  Action (fun ctx -> async {return ctx})


let need targets =
  Action (fun x ->
    async {
      //let! tt = targets
      printfn "need([%A]) in %A" targets x
    })

let steps = fun filename -> action {
  let! a = async {return 123}
  let! c = async {return 3}
  printfn "after ac"

  let f = a+c

  printfn "before need %A" f
  do! need ["def"; "dd"]

  let! ctx = getCtx
  printfn "ctx: %A" ctx

  let! d = async {return f}

  printfn "after need"

  do! Async.Sleep (11)

  printfn "after sleep"
}

Async.RunSynchronously(runAction (steps "m.c") "abc")

let stepsUnwrapped1 = fun fname ->
  action.Bind(async {return 123}, fun a ->
    action.Bind(async {return 3}, fun c ->
      let f = a + c
      action.Zero()
    )
  )


let stepsUnwrapped = fun fname ->
  action.Need(
    action.Bind(async {return 123}, fun a ->
    action.Bind(async {return 3}, fun c ->
      let f = a + c
      action.Zero()
    ),
    ["abc"]
  )

//    action.Bind(async {return 123}, fun a ->
//      action.Bind(async {return 3}, fun c ->
//        
//          Action (fun _ ->
//            let d = a + c
//            printf "c = %A" d
//            action.Bind(
//              Async.Sleep(11), fun () ->
//                action.Zero()
//            )
//          )
//        ,["abc"]))
//  ))


Async.RunSynchronously(runAction (stepsUnwrapped "m.c") "abc")

