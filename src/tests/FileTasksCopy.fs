namespace Tests

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks

[<TestFixture>]
type ``Testing Copy task``() =
    inherit XakeTestBase("cp")

    let assertTrue: bool -> unit = Assert.True

    [<Test>]
    member x.``copies single file``() =

        if Directory.Exists "cptgt" then
            Directory.Delete ("cptgt", true)

        do xake x.TestOptions {
            rules [
                "main" => recipe {
                    do! need ["samplefile"]
                    do! Cp {CpArgs.Default with file = "samplefile"; todir = "cptgt"}
                }

                "samplefile" ..> writeText "hello world"
            ]
        }

        assertTrue <| File.Exists ("cptgt" </> "samplefile")

    [<Test>]
    member x.``copies folder flatten``() =
        ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["cpin/samplefile"]
                    do! Cp {CpArgs.Default with dir = "cpin"; todir = "cptgt"; flatten = true}
                }

                "cpin/samplefile" ..> writeText "hello world"
            ]
        }

        (File.Exists >> Assert.True) ("cptgt" </> "samplefile")

    [<Test>]
    member x.``copies folder no flatten``() =
        ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["cpin/a/samplefile"]
                    do! Cp {CpArgs.Default with dir = "cpin"; todir = "cptgt"; flatten = false}
                }

                "cpin/a/samplefile" ..> writeText "hello world"
            ]
        }

        assertTrue <| File.Exists ("cptgt" </> "cpin" </> "a" </> "samplefile")

    [<Test>]
    member x.``copies fileset NO flatten``() =
        ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["cpin/a/samplefile"]
                    do! Cp {
                      CpArgs.Default with
                        files = (fileset {basedir "cpin"; includes "**/*"})
                        todir = "cptgt"
                        flatten = false
                        }
                }

                "cpin/a/samplefile" ..> writeText "hello world"
            ]
        }

        assertTrue <| File.Exists ("cptgt" </> "a" </> "samplefile")

    [<Test>]
    member x.``copies fileset flatten``() =
        ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

        do xake x.TestOptions {
            rules [
                "main" => action {
                    do! need ["cpin/a/samplefile"]
                    do! cp {files !!"cpin/**/*"; todir "cptgt"; flatten}
                }

                "cpin/a/samplefile" ..> writeText "hello world"
            ]
        }

        assertTrue <| File.Exists ("cptgt" </> "samplefile")
