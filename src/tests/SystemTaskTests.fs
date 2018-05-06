module ``SystemTasksTests``

open NUnit.Framework

open Xake
open Xake.Tasks

type private File = System.IO.File

let TestOptions = {ExecOptions.Default with Threads = 1; Targets = ["main"]; ConLogLevel = Diag; FileLogLevel = Silent}


// TODO make correct test
[<Test; Platform("Win")>]
let ``shell``() =
    File.Delete("." </> ".xake")

    do xake TestOptions {
        rules [
            "main" => recipe {

                do! Shell {
                    ShellOptions.Default with
                        Command = "dir"; Args = ["*.*"]
                        WorkingDir = Some "."; UseClr = true; FailOnErrorLevel = true} |> Recipe.Ignore
                
                let! error = shell {
                    cmd "dir"
                    args ["*.*"]
                    failonerror
                    workdir "."
                    }
                Assert.AreEqual (0, error)
            }
        ]
    }

    File.Exists "samplefile" |> Assert.False
