namespace Xake.Tasks.File

open Xake
open System.IO

[<AutoOpen>]
module RmImpl =

    type RmArgs = {
        dir: string
        file: string
        files: Fileset
        verbose: bool
    } with static member Default = {
            dir = null
            file = null
            files = Fileset.Empty
            verbose = false
        }

    let Del (args: RmArgs) =

        recipe {
            do! trace Level.Debug "Rm: args=%A" args

            // match args
            let! ctx = getCtx()
            let projectRoot = ctx.Options.ProjectRoot

            let reportDeleting file =
                if args.verbose then
                    ctx.Logger.Log Level.Message "[rm] deleting '%A'" file
                ctx.Logger.Log Level.Debug "deleting '%A'" file

            match args with
            | { files = f } when f <> Fileset.Empty ->
                ctx.Logger.Log Level.Message "[rm] %A" f
                let (Filelist files) = toFileList projectRoot f
                files |> List.map (fun f -> f.FullName)
                |> List.iter (fun file ->
                    do reportDeleting file
                    do File.Delete file
                )
            
            | { file = fileMask } when fileMask <> null ->
                ctx.Logger.Log Level.Message "[rm] %A" fileMask
                fileMask |> Path.parse |> Fileset.listByMask projectRoot
                |> Seq.iter (fun file ->
                    do reportDeleting file
                    do File.Delete file
                )

            | { dir = dirMask } when dirMask <> null ->
                dirMask |> Path.parseDir |> Fileset.listByMask projectRoot
                |> Seq.iter (fun dir ->
                    do reportDeleting dir
                    do Directory.Delete (dir, true)
                )

            | _ -> ()

            do! trace Level.Info "[rm] Completed"

            return ()
        }

    type DelArgsBuilder() =

        [<CustomOperation("file")>]    member this.File(a :RmArgs, value) =   {a with file = value }
        [<CustomOperation("dir")>]     member this.Dir(a :RmArgs, value) =    {a with dir = value}
        [<CustomOperation("files")>] member this.Fileset(a :RmArgs, value)= {a with files = value}
        [<CustomOperation("verbose")>] member this.Verbose(a:RmArgs) =        {a with verbose = true}

        member this.Bind(x, f) = f x
        member this.Yield(()) = RmArgs.Default
        member x.For(sq, b) = for e in sq do b e

        member this.Zero() = RmArgs.Default
        member this.Run(args:RmArgs) = Del args

    let del = DelArgsBuilder()