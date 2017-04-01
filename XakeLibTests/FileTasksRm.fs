module FileTasksRm

open NUnit.Framework

open Xake
open Xake.Tasks.File

type private File = System.IO.File

let TestOptions = {ExecOptions.Default with Threads = 1; Targets = ["main"]; ConLogLevel = Diag; FileLogLevel = Silent}


[<Test>]
let ``Rm deletes single file``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["samplefile"]
                File.Exists "samplefile" |> Assert.True
                do! del {file "samplefile"}
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

                do! del {file "samplefile*"}
            }

            "samplefile" ..> writeTextFile "hello world"
            "samplefile1" ..> writeTextFile "hello world1"
        ]
    }

    File.Exists "samplefile" |> Assert.False
    File.Exists "samplefile1" |> Assert.False

[<Test>]
let ``Rm deletes dir``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => recipe {
                // do System.IO.Directory.CreateDirectory("a\\b") |> ignore
                do! need ["a/samplefile"; "a/b/samplefile1"]
                File.Exists "a\\b\\samplefile1" |> Assert.True

                do! del {dir "a"}
            }

            "a/samplefile" ..> writeTextFile "hello world"
            "a/b/samplefile1" ..> writeTextFile "hello world1"
        ]
    }

    System.IO.Directory.Exists "a" |> Assert.False
