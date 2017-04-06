namespace Xake.Tasks.File

open Xake
open System.IO
open Xake.FileTasksImpl

[<AutoOpen>]
module CopyImpl =

    type CopyArgs = {
        dir: string
        file: string
        files: Fileset
        todir: string
        flatten: bool
        verbose: bool
        overwrite: bool
        dryrun: bool
    } with static member Default = {
            dir = null
            file = null
            files = Fileset.Empty
            todir = null
            flatten = false
            verbose = false
            overwrite = false
            dryrun = false
        }

    let Copy (args: CopyArgs) = recipe {
        do! trace Level.Debug "Copy: args=%A" args        

        let! ctx = getCtx()

        let copyFile target file =
            let fullname = file |> File.getFullName
            let tofile = target </> (file |> File.getFileName)

            if args.verbose then
                ctx.Logger.Log Level.Message "[copy] '%A' -> '%s'" fullname tofile
            ctx.Logger.Log Level.Debug "copying '%A' -> %s" fullname tofile

            if not args.dryrun then
                ensureDirCreated tofile
                File.Copy(fullname, tofile, true)

        let projectRoot = ctx.Options.ProjectRoot
        let targetDir = args.todir |> function | null -> projectRoot | s -> System.IO.Path.Combine(projectRoot, s)

        // TODO flatten
        // TODO overwrite        

        match args with
        | { files = f } when f <> Fileset.Empty ->
            ctx.Logger.Log Level.Message "[copy] fileset"
            let (Filelist files) = toFileList projectRoot f
            files |> List.iter (copyFile targetDir)
        
        | { file = fileMask } when fileMask <> null ->
            ctx.Logger.Log Level.Message "[copy] %A" fileMask
            let (Filelist files) = !!fileMask |> (toFileList projectRoot)
            files |> List.iter (copyFile targetDir)

        | { dir = dirMask } when dirMask <> null ->
            let fileMask = dirMask </> "**/*.*"
            let (Filelist files) = !!fileMask |> (toFileList projectRoot)
            files |> List.iter (copyFile targetDir)

        | _ -> ()
        do! trace Level.Info "[copy] Completed"    
        return ()
    }

    /// <summary>
    /// Copies one file to another location.
    /// </summary>
    /// <param name="src">Source file name</param>
    /// <param name="tgt">Target file location and name.</param>
    let copyFile (src: string) tgt = recipe {
        do! need [src]
        do! trace Level.Info "[copyFile] '%A' -> '%s'" src tgt

        do ensureDirCreated tgt

        File.Copy(src, tgt, true)
    }

    /// <summary>
    /// Requests the file and writes under specific name
    /// </summary>
    let copyFrom (src: string) = recipe {
        let! tgt = getTargetFullName()
        do! copyFile src tgt
    }
