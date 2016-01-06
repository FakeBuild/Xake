namespace Xake

module File =
    [<Sealed>]
    type T =
        interface System.IComparable
        override Equals: obj -> bool
        override GetHashCode: unit -> int

        member Name : string
        member FullName : string

    val make : string -> T
    val getName: T -> string
    val getFullName: T -> string
    val exists: T -> bool
    val getLastWriteTime: T -> System.DateTime

    val undefined: T

