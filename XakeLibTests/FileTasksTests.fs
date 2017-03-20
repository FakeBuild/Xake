module ``File tasks module``

open NUnit.Framework

open Xake
open Xake.FileTasksOld

type private File = System.IO.File

let TestOptions = {ExecOptions.Default with Threads = 1; Targets = ["main"]; ConLogLevel = Chatty; FileLogLevel = Silent}

[<Test>]
let ``allows delete file``() =
    "." </> ".xake" |> File.Delete

    let execCount = ref 0
    do xake TestOptions {
        rules [
            "main" => action {
                execCount := !execCount + 1
                do! need ["samplefile"]
                File.Exists "samplefile" |> Assert.True
                do! rm ["samplefile"]
            }

            "samplefile" ..> writeTextFile "hello world"
        ]
    }

    Assert.AreEqual(1, !execCount)
    File.Exists "samplefile" |> Assert.False

[<Test>]
let ``allows delete file by mask``() =
    "." </> ".xake" |> File.Delete
    let execCount = ref 0
    
    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["$$1"; "$$2"]
                File.Exists "$$2" |> Assert.True
                do! rm ["$$*"]
                execCount := !execCount + 1
            }

            "$$*" ..> writeTextFile "hello world"
        ]
    }

    Assert.AreEqual(1, !execCount)
    ["$$1"; "$$2"] |> List.iter (File.Exists >> Assert.False)

[<Test>]
let ``allows to delete by several masks``() =
    "." </> ".xake" |> File.Delete
    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["$aa"; "$bb"]
                File.Exists ("$bb") |> Assert.True
                do! rm ["$aa"; "$b*"]
            }

            "$*" ..> writeTextFile "hello world"
        ]
    }

    ["$aa"; "$bb"] |> List.iter (File.Exists >> Assert.False)

[<Test>]
let ``supports simple file copy``() =
    "." </> ".xake" |> File.Delete
    do xake TestOptions {
        rules [
            "main" => action {
                do! trace Error "Running inside 'main' rule"
                do! need ["aaa"; "clean"]
                do! copyFile "aaa" "aaa-copy"
            }

            "clean" => rm ["aaa-copy"]
            "aaa" ..> writeTextFile "hello world"
        ]
    }

    File.Exists "aaa-copy" |> Assert.True

open Xake.FileTasks

[<Test>]
let ``new modules``() =
    printfn "%s" Cp

    do xake TestOptions {
        rules [
            "main" => recipe {
                do! Rm {RmArgs.Default with file = "aaa.cs"}
                do! Rm {RmArgs.Default with dir = "dummy"}
                do! Rm {RmArgs.Default with files = fileset {includes "**/*.tmp"}; verbose = true}

                do! rm {file "aaa.cs"}
                do! rm {dir "aaa"}
                do! rm {files !! "**/*.tmp"}
                do! rm {
                    files (fileset {includes "**/*.tmp"})

                    verbose
                    includeemptydirs true
                }
            }
        ]
    }

[<Test>]
let ``Rm deletes single file``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["samplefile"]
                File.Exists "samplefile" |> Assert.True
                do! rm {file "samplefile"}
            }

            "samplefile" ..> writeTextFile "hello world"
        ]
    }

    File.Exists "samplefile" |> Assert.False

[<Test>]
let ``Rm deletes files by mask``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["samplefile"; "samplefile1"]
                File.Exists "samplefile" |> Assert.True
                File.Exists "samplefile1" |> Assert.True
                do! rm {file "samplefile*"}
            }

            "samplefile" ..> writeTextFile "hello world"
            "samplefile1" ..> writeTextFile "hello world1"
        ]
    }

    File.Exists "samplefile" |> Assert.False
    File.Exists "samplefile1" |> Assert.False

[<Test>]
let ``Rm deletes files in dir``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["a/samplefile"; "a/b/samplefile1"]
                File.Exists "a/b/samplefile1" |> Assert.True
                do! rm {dir "a"}
            }

            "a/samplefile" ..> writeTextFile "hello world"
            "a/b/samplefile1" ..> writeTextFile "hello world1"
        ]
    }

    System.IO.Directory.Exists "a" |> Assert.False

    
[<Test>]
let ``Rm deletes empty dirs in dir``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["a/b/samplefile1"]
                do! rm {files !! "a/**/*"; includeemptydirs true}
            }

            "a/b/samplefile1" ..> writeTextFile "hello world1"
        ]
    }

    System.IO.Directory.Exists "a" |> Assert.False
