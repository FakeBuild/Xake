[<AutoOpen>]
module Xake.Util

let (><) f a b = f b a
let inline (|OtherwiseFail|) _ = failwith "no choice"
let inline (|OtherwiseFailErr|) message _ = failwith message

type 't Agent = 't MailboxProcessor

[<Measure>] type ms

type private CacheKey<'K> = K of 'K

/// <summary>
/// Creates a memoized function with the same signature. Performs memoization by storing run results to a cache.
/// </summary>
/// <param name="f"></param>
let memoize f =
    let cache = ref Map.empty
    let lck = System.Object()
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


/// Memoizes the recursive function. Memoized function is passed as first argument to f.
///  * `f` - parameter of type `('a -> 'b) -> 'a -> 'b` The function to be memoized.
let memoizeRec f =
    let rec fn x = f fm x
    and fm = fn |> memoize
    in
    fm

/// Takes n first elements from a list.
let take cnt =
    if cnt > 0 then List.chunkBySize cnt >> List.head else fun _ -> List.empty

/// <summary>
/// Returns a list of unique values for a specific list.
/// </summary>
/// <param name="ls"></param>
let distinct ls =        
    ls |>
    List.fold (fun map item -> if map |> Map.containsKey item then map else map |> Map.add item 1) Map.empty
    |> Map.toList |> List.map fst
