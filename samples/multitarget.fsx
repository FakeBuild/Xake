// #r "../bin/Xake.dll"
#r "../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet
open System.IO

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/b/app.txt"]
            do! need ["a/b/bin/app.exe"]
            do! need ["a/b/bin/app.xml"]
            do! rm {dir "a"}
        }

        // group rule
        // challenge: how to identify common part for both names
        ["a/**/bin/*.exe"; "a/**/bin/*.xml"; "a/**/*.txt"] *..> recipe {
            let! mainTarget = getTargetFullName()
            let! [target1; target2; target3] = getTargetFiles()
            do! writeText "hello world"
            do! trace Message "main target is %A" mainTarget
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
            do File.WriteAllText(target3.FullName, "file3")
        }

        ["(src:a/**)/bin/*.exe"; "(src:a/**)/bin/*.xml"] *..> recipe {
            let! [_; xmlfile] = getTargetFiles()
            let! srcFolder = getRuleMatch "src"
            do! csc {
                src !!(srcFolder </> "*.cs")
                args ["--doc:" + xmlfile.FullName]
            }
        }
    ]
}