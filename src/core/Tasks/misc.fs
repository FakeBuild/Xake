namespace Xake.Tasks

open Xake
open System.IO

[<AutoOpen>]
module MiscImpl =

    /// <summary>
    /// Writes text to a file.
    /// </summary>
    let writeTargetText content = recipe {
        let! fileName = getTargetFullName()
        do ensureDirCreated fileName
        do File.WriteAllText(fileName, content)
    }

    let writeText content = writeTargetText content

    /// <summary>
    /// Writes binary data to a file.
    /// </summary>
    let writeTargetBytes content = recipe {
        let! fileName = getTargetFullName()
        do ensureDirCreated fileName
        do File.WriteAllBytes(fileName, content)
    }

    let readText path = recipe {
        do! need [path]
        let content = File.ReadAllText path
        return content
    }
