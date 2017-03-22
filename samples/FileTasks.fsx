#r "../bin/Xake.Core.dll"

open Xake
open Xake.FileTasks

type RmArgsBuilder() =

    [<DefaultValue>] val mutable file: string
    [<DefaultValue>] val mutable dir: string
    [<DefaultValue>] val mutable fset: FilesetBuilder

    [<CustomOperation("file")>]    member this.File(a :RmArgsBuilder,value) =   this.file <- value; ()
    [<CustomOperation("dir")>]     member this.Dir(a :RmArgsBuilder,value) =    this.dir <- value; ()

    [<CustomOperation("files")>]   member this.Fileset(a :RmArgsBuilder) =
        let fs = new FilesetBuilder()
        // {a with file = null; dir = null; fileset = fs}
        this.fset <- fs
        fs


    // member this.Bind(x, f) = f x

    // member this.Bind(x, f) = f x
    member this.Yield(()) = ()
    member x.Quote(c) = c

    // member x.Combine(a,b) =
    //     printfn "se"
    //     a
    member x.Zero() = ()
    member x.Delay(f : unit -> unit) = f

    // member this.Yield (txt : string) =
    //     printfn "yield txt"
    //     this.file <- this.file + "$" + txt
    member x.Run(c) =
        printfn "run"
        c
    // member x.For(sq, b) =
    //     printfn "for"
    //     for e in sq do b e

let rm = RmArgsBuilder()

let c = rm {
    // files (fileset {includes "**/*.tmp"})
    //file "aa.c"
    "d"
//    files { "ddd" }
    // dir "c"
}

printf "%A" c

type Rm1ArgsBuilder() =

    [<CustomOperation("file")>]    member this.File(a :RmArgs, value) =   {a with file = value; dir = null; fileset = Fileset.Empty}
    [<CustomOperation("dir")>]     member this.Dir(a :RmArgs, value) =    {a with file = null; dir = value; fileset = Fileset.Empty}
    [<CustomOperation("files")>]   member this.Files(a :RmArgs, value) =  {a with file = null; dir = null; fileset = value}
    [<CustomOperation("verbose")>] member this.Verbose(a:RmArgs) =        {a with verbose = true}

    member this.Bind(x, f) = f x
    member this.Yield(()) = RmArgs.Default

    member this.Yield (txt : string) =
        printfn "yield txt"
        {RmArgs.Default with file = txt}

    member this.ReturnFrom (txt : string) =
        printfn "ret txt"
        {RmArgs.Default with file = txt}

    member this.Zero() = RmArgs.Default
    member this.Run(args:RmArgs) = args

    // member this.Quote() = ()
    member this.Combine(a, b: RmArgs) =
        printfn "combine"
        {a with dir = b.dir}

    member this.Delay(f) = 
        printfn "Delay"
        f()
let rm1 = Rm1ArgsBuilder()

let d = rm1 {
    // files (fileset {includes "**/*.tmp"})
   // file "ddd"
   file "c"
   dir "d"
   files (fileset {includes "**/*.tmp"})
}

printf "%A" d

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
            // do! rm {
            //      // files (fileset {includes "**/*.tmp"})
            //      dir "c"
            // }
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
