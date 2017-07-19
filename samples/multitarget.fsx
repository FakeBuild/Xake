#r "../bin/Xake.dll"

open Xake
open Xake.Tasks
open System.IO

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/b/bin/app.exe"]
            do! need ["a/b/bin/app.xml"]
            do! need ["a/b/app.txt"]
            do! rm {dir "a"}
        }

        // group rule
        // challenge: how to identify common part for both names
        ["a/**/bin/*.exe"; "a/**/bin/*.xml"; "a/**/*.txt"] *..> recipe {
            let! [target1; target2; target3] = getTargetFiles()
            do! writeText "hello world"
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
            do File.WriteAllText(target3.FullName, "file3")
        }
    ]
}