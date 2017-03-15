namespace Xake.FileTasks

open Xake

[<AutoOpen>]
module RmImpl =

    type RmArgs = {
        dir: string
        file: string
        fileset: Fileset
        verbose: bool
    } with static member Default = {
        dir = null
        file = null
        fileset = Fileset.Empty
        verbose = false
    }

    let Rm (args: RmArgs) = recipe {
        // TODO implement
        do! trace Info "Rm: args=%A" args

        return ()
    }

    type RmArgsBuilder() =

        [<CustomOperation("file")>]    member this.File(a :RmArgs, value) =   {a with file = value; dir = null; fileset = Fileset.Empty}
        [<CustomOperation("dir")>]     member this.Dir(a :RmArgs, value) =    {a with file = null; dir = value; fileset = Fileset.Empty}
        [<CustomOperation("files")>]   member this.Files(a :RmArgs, value) =  {a with file = null; dir = null; fileset = value}
        [<CustomOperation("verbose")>] member this.Verbose(a:RmArgs) =        {a with verbose = true}

        member this.Bind(x, f) = f x
        member this.Yield(()) = RmArgs.Default
        member this.For(x, f) = f x

        member this.Zero() = RmArgs.Default
        member this.Run(args:RmArgs) = Rm args

    let rm = RmArgsBuilder()