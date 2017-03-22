namespace Xake.FileTasks

open Xake

[<AutoOpen>]
module RmImpl =

    type RmArgs = {
        dir: string
        file: string
        files: Fileset
        verbose: bool
        includeemptydirs: bool
    } with static member Default = {
            dir = null
            file = null
            files = Fileset.Empty
            verbose = false
            includeemptydirs = true
        }

    let Rm (args: RmArgs) =

        recipe {
            // TODO implement
            do! trace Level.Debug "Rm: args=%A" args

            failwith "Not implemented"

            // match args
            let! ctx = getCtx()
            let projectRoot = ctx.Options.ProjectRoot

            let reportDeleting file =
                if args.verbose then
                    ctx.Logger.Log Level.Message "[rm] deleting '%A'" file
                ctx.Logger.Log Level.Debug "deleting '%A'" file

            let deleteFile (file: string) =
                // do System.IO.File.Delete file
                do reportDeleting file

            let fileset =
                match args with
                | { files = f } when f <> Fileset.Empty ->     f
                | { file = fileMask } when fileMask <> null -> !! fileMask
                | { dir = dirMask } when dirMask <> null ->    !! (dirMask </> "**/*.*")
                | _ -> Fileset.Empty

            // TODO empty dirs

            do! trace Level.Info "[rm] %A" fileset
            // let (Filelist files) = toFileList projectRoot fileset
            // files |> List.map (fun f -> f.FullName) |> List.iter deleteFile

            do! trace Level.Info "[rm] Completed"

            return ()
        }

    type RmArgsBuilder() =

        [<CustomOperation("file")>]    member this.File(a :RmArgs, value) =   {a with file = value }
        [<CustomOperation("dir")>]     member this.Dir(a :RmArgs, value) =    {a with dir = value}
        [<CustomOperation("files")>] member this.Fileset(a :RmArgs, value)= {a with files = value}
        [<CustomOperation("verbose")>] member this.Verbose(a:RmArgs) =        {a with verbose = true}
        [<CustomOperation("includeemptydirs")>] member this.Includeemptydirs(a:RmArgs, includeEmpty) = {a with includeemptydirs = includeEmpty}

        member this.Bind(x, f) = f x
        member this.Yield(()) = RmArgs.Default
        member x.For(sq, b) = for e in sq do b e

        member this.Zero() = RmArgs.Default
        member this.Run(args:RmArgs) = Rm args

    let rm = RmArgsBuilder()