#r "paket: nuget Xake ~> 1.1 prerelease //"

open Xake
open Xake.Tasks

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/samplefile"; "a/b/samplefile1"]
            do! rm {dir "a"}
            // TODO more samples to come
        }

        "a/samplefile" ..> writeText "hello world"
        "a/b/samplefile1" ..> writeText "this is another file"
    ]
}