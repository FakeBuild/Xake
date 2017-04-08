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

    module internal impl =
        let makeRelPath (root: string) =
            let rootLen = root.Length
            if rootLen <= 0 then id
            else
                let root', rootLen' =
                    if root.[rootLen - 1] = Path.DirectorySeparatorChar then
                        root, rootLen
                    else
                        root + (System.String (Path.DirectorySeparatorChar, 1)), rootLen + 1

                fun (path: string) ->
                    if path.StartsWith root' then
                        path.Substring rootLen'
                    else
                        path

        let getBasedirFullPath projectRoot fileset =
            let (Fileset ({BaseDir = basedir'}, _)) = fileset

            let basedir = basedir' |> function | None -> projectRoot | Some s -> Path.Combine(projectRoot, s)
            FileInfo(basedir).FullName

    let Copy (args: CopyArgs) = recipe {
        do! trace Level.Debug "Copy: args=%A" args

        let! ctx = getCtx()

        let copyFile target getRelativePath file =
            let fullname = file |> File.getFullName
            let tofile = target </> (getRelativePath file)

            if args.verbose then
                ctx.Logger.Log Level.Message "[copy] '%A' -> '%s'" fullname tofile
            ctx.Logger.Log Level.Debug "copying '%A' -> %s" fullname tofile

            if not args.dryrun then
                ensureDirCreated tofile
                File.Copy(fullname, tofile, true)

        let projectRoot = ctx.Options.ProjectRoot
        let targetDir = args.todir |> function | null -> projectRoot | s -> System.IO.Path.Combine(projectRoot, s)

        // TODO overwrite

        let fileset =
            args
            |> function
            | { files = f } when f <> Fileset.Empty ->
                ctx.Logger.Log Level.Message "[copy] fileset"
                f
            | { file = fileMask } when fileMask <> null ->
                ctx.Logger.Log Level.Message "[copy] %A" fileMask
                !!fileMask
            | { dir = dirMask } when dirMask <> null ->
                !!(dirMask </> "**/*.*")
            | _ -> Fileset.Empty

        let getRelativePath = args.flatten |> function
            |true -> File.getFileName
            | _ ->
                let baseFullPath = impl.getBasedirFullPath projectRoot fileset
                File.getFullName >> (impl.makeRelPath baseFullPath)

        // let (Filelist files) = fileset |> (toFileList projectRoot)
        // for file in files do
        //     copyFile targetDir getRelativePath file

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
