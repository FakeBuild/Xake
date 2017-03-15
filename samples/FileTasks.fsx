#r "../bin/Xake.Core.dll"

open Xake
open Xake.FileTasks

type RmArgsBuilder() =

    let mutable args = RmArgs.Default

    [<CustomOperation("file")>]    member this.File(value) =   args <- {args with file = value; dir = null; fileset = Fileset.Empty}
    [<CustomOperation("dir")>]     member this.Dir(value) =    args <- {args with file = null; dir = value; fileset = Fileset.Empty}
    [<CustomOperation("files")>]   member this.Files(value) =  args <- {args with file = null; dir = null; fileset = value}
    [<CustomOperation("verbose")>] member this.Verbose() =     args <- {args with verbose = true}

    // member this.Bind(x, f) = f x
    // member this.Yield(()) = RmArgs.Default
    // // member x.For(sq, b) = for e in sq do b e

    // member this.Zero() = RmArgs.Default
    // member this.Run(args:RmArgs) = Rm args

    member x.Zero() = ()
    member x.Delay(f : unit -> unit) = f

    member x.set(s: string) = 
        args <- {args with file = s}

let rm = RmArgsBuilder()

do xakeScript {
   rules [
        "main" => recipe {
            do! alwaysRerun()
            do! trace Info "Starting"
            // do! Rm {RmArgs.Default with file = "aaa.cs"}
            // do! Rm {RmArgs.Default with dir = "dummy"}
            // do! Rm {RmArgs.Default with fileset = fileset {includes "**/*.tmp"}; verbose = true}

            // do! rm {file "aaa.cs"}
            // do! rm {dir "aaa"}
            do! rm {
                 // files (fileset {includes "**/*.tmp"})
                 dir "c"
            }
            // do! rm {
            //     fileset {includes "**/*.tmp"}
            //     fileset {includes "**/*.tmp_"}
            //     file "aaa.cs"
            //     file "bbb.cs"
            //     dir "aaa"

            //     verbose
            // }
        }
    ]
}
