[<System.Obsolete("Use Xake.Tasks.File instead")>]
[<AutoOpen>]
module Xake.FileTasks

open System.IO
open Xake
open Xake.FileTasksImpl

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
[<System.Obsolete("Use Xake.Tasks.cp instead")>]
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
