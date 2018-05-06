namespace Tests

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks

[<TestFixture>]
type ``File tasks module``() =
    inherit XakeTestBase("files")

    [<Test>]
    member x.``allows delete file``() =

        let execCount = ref 0
        do xake x.TestOptions {
            rules [
                "main" => action {
                    execCount := !execCount + 1
                    do! need ["samplefile"]
                    File.Exists "samplefile" |> Assert.True
                    do! rm {file "samplefile"}
                }

                "samplefile" ..> writeText "hello world del"
            ]
        }

        Assert.AreEqual(1, !execCount)
        File.Exists "samplefile" |> Assert.False

    [<Test>]
    member x.``allows delete file by mask``() =
        let execCount = ref 0
        
        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["$$1"; "$$2"]
                    File.Exists "$$2" |> Assert.True
                    do! rm {file "$$*"}
                    execCount := !execCount + 1
                }

                "$$*" ..> writeText "hello world"
            ]
        }

        Assert.AreEqual(1, !execCount)
        ["$$1"; "$$2"] |> List.iter (File.Exists >> Assert.False)

    [<Test>]
    member x.``allows to delete by several masks``() =
        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["$aa"; "$bb"]
                    File.Exists ("$bb") |> Assert.True
                    do! rm {file "$aa"}
                    do! rm {file "$b*"}
                }

                "$*" ..> writeText "hello world"
            ]
        }

        ["$aa"; "$bb"] |> List.iter (File.Exists >> Assert.False)

    [<Test>]
    member x.``supports simple file copy``() =
        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! trace Error "Running inside 'main' rule"
                    do! need ["aaa"; "clean"]
                    do! copyFile "aaa" "aaa-copy"
                }

                "clean" => rm {file "aaa-copy"}
                "aaa" ..> writeText "hello world"
            ]
        }

        File.Exists "aaa-copy" |> Assert.True
