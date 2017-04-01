namespace Xake.Tasks.File

open Xake

[<AutoOpen>]
module CopyImpl =

    let Copy = "cp task goes here"

    /// <summary>
    /// Requests the file and writes under specific name
    /// </summary>
    let copyFrom (src: string) =
        recipe {
            let! tgt = getTargetFullName()
            // do! copyFile src tgt
            return ()
        }
