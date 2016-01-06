namespace Xake

module File =
    [<Sealed>]
    type T =
        interface System.IComparable
        override Equals: obj -> bool
        override GetHashCode: unit -> int

        member Name : string
        member FullName : string

    /// Makes a new File instance by a file pathname.
    val make : string -> T

    /// Gets the file name. Short and user-friendly
    val getName: T -> string

    /// Gets fully qualified file name.
    val getFullName: T -> string

    /// Get true if file exists
    val exists: T -> bool

    /// Get the file modification time
    val getLastWriteTime: T -> System.DateTime

    /// Get the instance indicating the file is not specified.
    val undefined: T

