module ``File tasks module``

open NUnit.Framework

open Xake
open Xake.FileTasks

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
                Assert.IsTrue <| File.Exists ("samplefile")
                do! rm ["samplefile"]
            }

            "samplefile" *> fun file -> action {
                File.WriteAllText(file.FullName, "hello world")
            }
        ]
    }

    Assert.AreEqual(1, !execCount)
    Assert.IsFalse <| File.Exists ("samplefile")

[<Test>]
let ``allows delete file by mask``() =
    "." </> ".xake" |> File.Delete
    let execCount = ref 0
    
    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["$$1"; "$$2"]
                Assert.IsTrue <| File.Exists ("$$2")
                do! rm ["$$*"]
                execCount := !execCount + 1
            }

            "$$*" *> fun file -> action {
                File.WriteAllText(file.FullName, "hello world")
            }
        ]
    }

    Assert.AreEqual(1, !execCount)
    ["$$1"; "$$2"] |> List.iter (Assert.IsFalse << File.Exists)

[<Test>]
let ``allows to delete by several masks``() =
    "." </> ".xake" |> File.Delete
    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["$aa"; "$bb"]
                Assert.IsTrue <| File.Exists ("$bb")
                do! rm ["$aa"; "$b*"]
            }

            "$*" *> fun file -> action {
                File.WriteAllText(file.FullName, "hello world")
            }
        ]
    }

    ["$aa"; "$bb"] |> List.iter (Assert.IsFalse << File.Exists)

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

            "clean" => action {
                do! rm ["aaa-copy"]
            }

            "aaa" *> fun file -> action {
                File.WriteAllText(file.FullName, "hello world")
            }
        ]
    }

    Assert.IsTrue <| File.Exists ("aaa-copy")
