namespace Tests

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks

[<TestFixture>]
type ``Testing Rm``() =
    inherit XakeTestBase("rm")

    [<Test>]
    member x.``Rm deletes single file``() =

        do xake x.TestOptions {
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
    member x.``Rm deletes files by mask``() =

        do xake x.TestOptions {
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
    member x.``Rm deletes dir``() =
        do xake x.TestOptions {
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
    member x.``Rm deletes fileset``() =

        do xake x.TestOptions {
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
