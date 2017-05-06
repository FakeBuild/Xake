module ``SystemTasksTests``

open NUnit.Framework

open Xake
open Xake.SystemTasks

type private File = System.IO.File

let TestOptions = {ExecOptions.Default with Threads = 1; Targets = ["main"]; ConLogLevel = Diag; FileLogLevel = Silent}


[<Test; Platform("windows")>]
let ``shell``() =
    "." </> ".xake" |> File.Delete

    do xake TestOptions {
        rules [
            "main" => recipe {
                let! error = shell {
                    cmd "dir"
                    useclr
                    failonerror
                    // workdir "."
                    }
                Assert.AreEqual (0, error)
            }
        ]
    }

    File.Exists "samplefile" |> Assert.False
