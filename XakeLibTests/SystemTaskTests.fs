module ``System task``

open NUnit.Framework

open Xake
open Xake.SystemTasks

type private File = System.IO.File

let TestOptions = {ExecOptions.Default with Threads = 1; Targets = ["main"]; ConLogLevel = Diag; FileLogLevel = Silent}


//[<Test>]
let ``system action``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => recipe {
                do! need ["samplefile"]
                File.Exists "samplefile" |> Assert.True

                let! error = sys {
                    cmd "dir"
                    useclr
                    failonerror
                    dir "bin"}
                let! xx = Sys {SysOptions.Default with Command = "dir"}
                Assert.AreEqual (0, error)
            }

            "samplefile" ..> writeTextFile "hello world"
        ]
    }

    File.Exists "samplefile" |> Assert.False
