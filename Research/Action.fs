module Research.Action

// expression type
type Action<'a,'b> = 'a -> Async<'b>

type ActionBuilder<'x>() =
  let a = async
  
  member __.Zero() :Action<'x,_>    = fun _ -> a.Zero()
  member __.Delay(f) :Action<'x,'b> = fun x -> f() x
  member __.Return(c)               = fun _ -> a.Return(c)  
  member __.Run(q) = q

  member __.Bind(m, f):Action<'x,_> = fun x -> async {let! a = m in return! f a x}
  member __.For(source, f) : Action<'x,'b> = fun x -> async {let! a = source x in return! f a x}

  [<CustomOperation("need")>]
  member __.Need(s:Action<'x,'a>, targets: string list) :Action<'x,'a> =
    fun x ->
      let r = s x in
      printfn "calling need with %A (%A)" targets x
      async {
        do! Async.Sleep 400
        return! r
      }

  member this.Yield(dd) =
    printfn "yield %A" dd
    this.Zero()
    //async {dd}

let action = ActionBuilder<string>()

let steps = fun filename -> action {
  let! a = async {return 123}
  let! c = async {return 3}

  printfn "after ac"
  let d = a + c
  let f:string = "fff"

  printfn "before need %A" f
  need ["def"; "dd"]

  printfn "after need"

  do! Async.Sleep (11)

  printfn "after sleep"
}
// let! a = fn()
// let! b = fn2(a)
// return b

Async.RunSynchronously(steps "m.c" "abc")