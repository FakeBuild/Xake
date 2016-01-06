namespace Xake

module File =

    type String = System.String

    (*
        Need a file type which:
         * effectively handles relativity to a root folder - WHY it's important? - for relocation of .xake?
         * could compare to other file regardless the way it's specified
         * could be both relative and absolute

         Rule is defined for files matching the path mask.
    *)

    type private BclFile = System.IO.File

    let private compareNames : string -> string -> int =
        let isUnix = Env.isUnix
        fun a b -> System.String.Compare(a, b, isUnix)

    let private getFileHash : string -> int =
        if Env.isUnix then
            fun name -> name |> hash
        else
            fun name -> name.ToLowerInvariant() |> hash

    [<CustomEquality; CustomComparison>]
    type T = T of string * System.IO.FileInfo with

        // Note the Name used in application output for better readability

        member f.Name = let (T (n,_)) = f in n
        member f.FullName = let (T (_,f)) = f in if f = null then "" else f.FullName

        interface System.IComparable with
            member x.CompareTo yobj =
                match yobj with
                | :? T as y -> compareNames (x.FullName) (y.FullName)
                | _ -> invalidArg "yobj" "cannot compare values of different types"

        override x.Equals(yobj) =
            match yobj with
            | :? T as y -> 0 = compareNames x.FullName y.FullName
            | _ -> false
        override x.GetHashCode() =
            x |> (function |undefined -> "" |_ -> x.FullName) |> getFileHash

    let make n =
        if String.IsNullOrWhiteSpace n then
            failwith "File name cannot be empty"

        T (n, new System.IO.FileInfo(n))

    let getName (f:T) = f.Name
    let getFullName (f:T) = f.FullName
    let exists (f:T) = BclFile.Exists f.FullName
    let getLastWriteTime (f:T) = BclFile.GetLastWriteTime f.FullName

    let undefined = T ("",null)
