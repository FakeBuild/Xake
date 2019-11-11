namespace Xake

// expression type
type Recipe<'a,'b> = Recipe of ('a -> Async<'a * 'b>)

module internal RecipeAlgebra =
    let runAction (Recipe r) = r
    let returnF a = Recipe (fun s -> async {return (s,a)})

    let bindF m f = Recipe (fun s -> async {
        let! (s', b) = runAction m s in
        return! runAction (f b) s'
        })
    let bindA m f = Recipe (fun s -> async {
        let! a = m in
        return! runAction (f a) s
        })
    let resultFromF m = m

    let callF f a = bindF (returnF a) f
    let delayF f = callF f ()

    let doneF = Recipe (fun s -> async {return (s,())})

    let ignoreF p = bindF p (fun _ -> doneF)
    let combineF f g = bindF f (fun _ -> g)

    let rec whileF guard prog =
        if not (guard()) then 
            doneF
        else 
            (fun () -> whileF guard prog) |> bindF prog

    let tryWithF body h = // (body:Recipe<'a,'b>) (h: 'exc ->Recipe<'a,'b>) : Recipe<'a,'b> =
        fun x -> async {
            try
                return! runAction body x
            with e ->
                return! runAction (h e) x
        } |> Recipe

    let tryFinallyF body comp = // (body:Recipe<'a,'b>) -> (comp: unit -> unit) -> Recipe<'a,'b> =
        fun x -> async {
            try
                return! runAction body x
            finally
                do comp()
        } |> Recipe

    let usingF (r:'T :> System.IDisposable) body =
        tryFinallyF (body r) (fun () -> r.Dispose())

    let forF (e: seq<_>) prog =
        usingF (e.GetEnumerator()) (fun e ->
            whileF
                (fun () -> e.MoveNext())
                ((fun () -> prog e.Current) |> delayF)
        )

//    [<CustomOperation("step")>]
//    member this.Step(m, name) =
//        printfn "STEP %A %A" m name
//        ()

[<AutoOpen>]
module Builder =
    open RecipeAlgebra
    type RecipeBuilder() =
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
    
    let action = RecipeBuilder()
    let recipe = RecipeBuilder()
