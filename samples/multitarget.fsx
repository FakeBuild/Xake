#r "../bin/Xake.dll"

open Xake
open Xake.Tasks
open System.IO

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/file2"]
            do! need ["a/file1"]
            do! need ["a/subfoldr/file1.exe"]
            do! rm {dir "a"}
        }

        // simple multitarget rule
        ("a", ["file1"; "file2"]) ..>> recipe {
            let! [target1; target2] = getTargetFiles()
            do! writeText "hello world"
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
        }

        // group rule
        // challenge: how to identify common part for both names
        ["a/**/bin/*.exe"; "a/**/bin/*.xml"; "a/**/*.txt"] ..>.. recipe {
            let! [target1; target2] = getTargetFiles()
            do! writeText "hello world"
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
        }
    ]
}