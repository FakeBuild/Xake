#r "../bin/Xake.Core.dll"

open Xake
open Xake.FileTasks

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/samplefile"; "a/b/samplefile1"]
            do! rm {dir "a"}
            // let dd = Path.parseDir "*"|> Fileset.listByMask opt.ProjectRoot
            // do! trace Level.Command "dd: %A" dd
        }

        "a/samplefile" ..> writeTextFile "hello world"
        "a/b/samplefile1" ..> writeTextFile "hello world1"
    ]
}