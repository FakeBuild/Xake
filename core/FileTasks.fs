[<AutoOpen>]
module Xake.FileTasks

open System.IO
open Xake

/// <summary>
/// Removes the files.
/// </summary>
/// <param name="names">List of files/filemasks to be removed.</param>
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
/// Copies one file to another location.
/// </summary>
/// <param name="src">Source file name</param>
/// <param name="tgt">Target file location and name.</param>
let copyFile (src: string) tgt =
    action {
        // TODO fail on error, normalize names
        do! need [src]
        do! trace Level.Info "[copyFile] '%A' -> '%s'" src tgt

        File.Copy(src, tgt, true)
    }

/// <summary>
/// Copies single file.
/// </summary>
[<System.Obsolete("Use copyFile instead. `cp` is reserved for future flexible and powerful solution.")>]
let cp = copyFile

/// <summary>
/// Copies multiple files specified by a mask to another location.
/// </summary>
[<System.Obsolete("Use with caution, the implementation is incomplete")>]
let copyFiles (src: string list) tgt_folder =

    // TODO check how should it process dependencies
    // TODO print the file name (in Verbose mode)
    // TODO currently if flattens target files layout so that
    // /src/**/*.c files will be stored without preserving folders structure.

    let copyFile tgt_folder (fi:FileInfo) =
        let tgt = Path.Combine(tgt_folder, fi.Name)
        File.Copy(fi.FullName, tgt, true)

    let copyByMask root mask =
        let (Filelist files) = Fileset.ls mask |> (toFileList root)
        files |> List.iter (copyFile tgt_folder)

    action {
        let! options = getCtxOptions()
        do! trace Level.Info "[copyFiles] '%A' -> '%s'" src tgt_folder

        src |> List.iter (copyByMask options.ProjectRoot)
    } 

