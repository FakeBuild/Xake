namespace Xake.Tasks.File

open Xake
open System.IO

[<AutoOpen>]
module MiscImpl =

    let internal ensureDirCreated fileName =
        let dir = fileName |> Path.GetDirectoryName

        if not <| System.String.IsNullOrEmpty(dir) then
            do dir |> Directory.CreateDirectory |> ignore

    /// <summary>
    /// Writes text to a file.
    /// </summary>
    let writeText content = recipe {
        let! fileName = getTargetFullName()
        do ensureDirCreated fileName
        do File.WriteAllText(fileName, content)
    }
