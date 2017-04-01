[<System.Obsolete("Use Xake.Tasks.File instead")>]
[<AutoOpen>]
module Xake.FileTasks

open System.IO
open Xake

let private ensureDirCreated fileName =
    let dir = fileName |> Path.GetDirectoryName

    if not <| System.String.IsNullOrEmpty(dir) then
        do dir |> Directory.CreateDirectory |> ignore

/// <summary>
/// Removes the files.
/// </summary>
/// <param name="names">List of files/filemasks to be removed.</param>
[<System.Obsolete("Use Xake.Tasks.File.del instead")>]
let rm (names : string list) =

    let deleteByMask root mask =
        let (Filelist files) = Fileset.ls mask |> (toFileList root)
        files |> List.map (fun f -> f.FullName) |> List.iter File.Delete
    
    action {
        do! trace Level.Info "[rm] '%A'" names
        let! options = getCtxOptions()

        names |> List.iter (deleteByMask options.ProjectRoot)
        do! trace Level.Info "[rm] Completed"
    }

/// <summary>
/// Writes text to a file.
/// </summary>
let writeTextFile content = recipe {
    let! fileName = getTargetFullName()
    do ensureDirCreated fileName
    do File.WriteAllText(fileName, content)
}

/// <summary>
/// Copies one file to another location.
/// </summary>
/// <param name="src">Source file name</param>
/// <param name="tgt">Target file location and name.</param>
[<System.Obsolete("Use Xake.Tasks.File.copy instead")>]
let copyFile (src: string) tgt =
    action {
        // TODO fail on error, normalize names
        do! need [src]
        do! trace Level.Info "[copyFile] '%A' -> '%s'" src tgt

        do ensureDirCreated tgt

        File.Copy(src, tgt, true)
    }

/// <summary>
/// Requests the file and writes under specific name
/// </summary>
let copyFrom (src: string) =
    recipe {
        let! tgt = getTargetFullName()
        do! copyFile src tgt
    }

/// <summary>
/// Copies multiple files specified by a mask to another location.
/// </summary>
[<System.Obsolete("Use Xake.Tasks.File.copy instead")>]
let copyFiles (src: string list) targetDir =

    // TODO check how should it process dependencies
    // TODO currently if flattens target files layout so that
    // /src/**/*.c files will be stored without preserving folders structure.

    action {
        let! options = getCtxOptions()
        do! trace Level.Info "[copyFiles] '%A' -> '%s'" src targetDir

        let! ctx = getCtx()
        let logVerbose = ctx.Logger.Log Verbose "%s"

        let copyFile target file =
            let tgt = Path.Combine(target, file |> File.getFileName)
            ctx.Logger.Log Verbose "Copying %s..." (file |> File.getName)
            File.Copy((File.getFullName file), tgt, true)

        let copyByMask root mask =
            let (Filelist files) = Fileset.ls mask |> (toFileList root)
            files |> List.iter (copyFile targetDir)

        src |> List.iter (copyByMask options.ProjectRoot)
    } 

