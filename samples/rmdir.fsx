// #r "../bin/Xake.Core.dll"
#r "../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/samplefile"; "a/b/samplefile1"]
            do! rm {dir "a"}
            // let dd = Path.parseDir "*"|> Fileset.listByMask opt.ProjectRoot
            // do! trace Level.Command "dd: %A" dd
        }

        "a/samplefile" ..> writeText "hello world"
        "a/b/samplefile1" ..> writeText "hello world1"
    ]
}