namespace Xake

[<Sealed>]
type File =
    interface System.IComparable
    override Equals: obj -> bool
    override GetHashCode: unit -> int

    member Name : string
    member FullName : string

    static member undefined: File

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module File =

    /// Makes a new File instance by a file pathname.
    val make : string -> File

    /// Gets the file name.
    val getFileName: File -> string

    /// Gets the file extension.
    val getFileExt: File -> string

    /// Gets the file direactory.
    val getDirName: File -> string

    /// Gets fully qualified file name.
    val getFullName: File -> string

    /// Get true if file exists
    val exists: File -> bool

    /// Get the file modification time
    val getLastWriteTime: File -> System.DateTime
