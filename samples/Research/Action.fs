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

      // binds both monadic and for async computations
      member this.Bind(m, f) = bind m f
      member this.Bind(m, f) = bindA m f

      member this.Combine(r1, r2) = bind r1 (fun () -> r2)
      member this.For(s:seq<_>, f)  = Action (fun x -> async {
        for i in s do runAction (f i) x |> ignore
        })

      // here's the attempt to implement 'need' operations

      [<CustomOperation("need")>]
      member this.Need(Action a,  targets: string list) =
        Action (fun x ->
          let r = a x
          printfn "need(%A, [%A])" a targets
          r)

       member this.For(a, f)  = bindA a f
       member this.Yield(()) =
        returnF ()

    let action = ActionBuilder<string>()

    /////////////////////////////////////////////////////////////
    // other functions for Action

    /// Gets action context
    let getCtx = Action (fun ctx -> async {return ctx})


    let needFn targets = action {
        let! ctx = getCtx
        printfn "need([%A]) in %A" targets ctx
        // TODO need body
      }

let steps = fun filename -> action {
  let! a = async {return 123}
  let! c = async {return 3}
  printfn "after ac"
  let f = a+c

  printfn "before need %A" f
  // need ["def"; "dd"]
  do! needFn ["def"; "dd"]
  printfn "after need"

  for i in [0..10] do
    //let! cc = getCtx
    do! Async.Sleep (1)   // TODO does not work!
    printfn "Hello"

  if 1=2 then 
    do! Async.Sleep (1)
    printfn "if 1"
  else
    do! Async.Sleep (20)
    printfn "if 2"

  let! ctx = getCtx
  printfn "ctx: %A" ctx

  let! d = async {return f}

  do! Async.Sleep (11)

  printfn "after sleep"
}

Async.RunSynchronously(runAction (steps "m.c") "abc")

    let program1 = fun filename -> action {
      let! a = async {return 123}
      let f = a+1

      // need ["def"; "dd"]
      do! needFn ["def"; "dd"]
      printfn "after need"

      for i in [0..10] do
        do! Async.Sleep (1)
        printfn "i: %A" i

      let! d = async {return f}
      let! ctx = getCtx
      printfn "ctx: %A, %A" ctx f
    }

    Async.RunSynchronously(runAction (program1 "m.c") "abc")

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
  ))

//Async.RunSynchronously(runAction (stepsUnwrapped "m.c") "abc")
