[<AutoOpen>]
module Common

type private CacheKey<'K> = K of 'K
let internal memoize f =
    let cache = ref Map.empty
    let lck = new System.Object()
    fun x ->
        match !cache |> Map.tryFind (K x) with
        | Some v -> v
        | None ->
            lock lck (fun () ->
                match !cache |> Map.tryFind (K x) with
                | Some v -> v
                | None ->
                    let res = f x
                    cache := !cache |> Map.add (K x) res
                    res)
