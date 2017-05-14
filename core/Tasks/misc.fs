namespace Xake.Tasks

open Xake
open System.IO

[<AutoOpen>]
module MiscImpl =

    open Xake.FileTasksImpl

    /// <summary>
    /// Writes text to a file.
    /// </summary>
    let writeText content = recipe {
        let! fileName = getTargetFullName()
        do ensureDirCreated fileName
        do File.WriteAllText(fileName, content)
    }
