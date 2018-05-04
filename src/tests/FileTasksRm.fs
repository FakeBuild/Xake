module ``Del(rm) task``

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks

let private rememberDir = Directory.GetCurrentDirectory()
let private outFolder = rememberDir </> "~testout~" </> "rm"

let TestOptions = {
    ExecOptions.Default with
        Threads = 1
        Targets = ["main"]
        ConLogLevel = Diag; FileLogLevel = Silent
        ProjectRoot = outFolder
    }

[<OneTimeSetUp>]
let setup () =
    Directory.CreateDirectory outFolder |> ignore
    Directory.SetCurrentDirectory outFolder

[<OneTimeTearDown>]
let teardown () =
    Directory.SetCurrentDirectory rememberDir

[<SetUp>]
let setupTest () =
    "." </> ".xake" |> File.Delete

[<Test>]
let ``Rm deletes single file``() =

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["samplefile"]
                File.Exists "samplefile" |> Assert.True
                do! rm {file "samplefile"; verbose}
            }

            "samplefile" ..> writeText "hello world"
        ]
    }

    (File.Exists >> Assert.False) "samplefile"

[<Test>]
let ``Rm deletes files by mask``() =

    do xake TestOptions {
        filelog "c:\\!\\logggg" Diag
        rules [
            "main" => action {
                do! need ["samplefile"; "samplefile1"]
                (File.Exists >> Assert.True) "samplefile"
                (File.Exists >> Assert.True) "samplefile1"

                do! rm {file "samplefile*"}
            }

            "samplefile" ..> writeText "hello world"
            "samplefile1" ..> writeText "hello world1"
        ]
    }

    (File.Exists >> Assert.False) "samplefile"
    (File.Exists >> Assert.False) "samplefile1"

[<Test>]
let ``Rm deletes dir``() =
    do xake TestOptions {
        rules [
            "main" => recipe {
                do! need ["a/samplefile"; "a/b/samplefile1"]
                File.Exists ("a" </> "b" </> "samplefile1") |> Assert.True

                do! rm {dir "a"}
            }

            "a/samplefile" ..> writeText "hello world"
            "a/b/samplefile1" ..> writeText "hello world1"
        ]
    }

    (Directory.Exists >> Assert.False) "a"


[<Test>]
let ``Rm deletes fileset``() =

    do xake TestOptions {
        rules [
            "main" => recipe {
                do! need ["samplefile"; "samplefile1"]
                do! rm {
                    files (fileset {
                        includes "samplefile*"
                    })
                }
            }

            "samplefile*" ..> writeText "hello world"
        ]
    }

    (File.Exists >> Assert.False) "samplefile"
    (File.Exists >> Assert.False) "samplefile1"
