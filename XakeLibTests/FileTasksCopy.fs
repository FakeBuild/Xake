module ``Copy task``

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks

let assertTrue: bool -> unit = Assert.True

let private rememberDir = Directory.GetCurrentDirectory()
let private outFolder = rememberDir </> "~testdata~" </> "cp"

let TestOptions = {
      ExecOptions.Default with
        Threads = 1; Targets = ["main"]; ConLogLevel = Diag; FileLogLevel = Silent
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
let ``copies single file``() =

    if Directory.Exists "cptgt" then
        Directory.Delete ("cptgt", true)

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["samplefile"]
                do! Cp {CpArgs.Default with file = "samplefile"; todir = "cptgt"}
            }

            "samplefile" ..> writeText "hello world"
        ]
    }

    assertTrue <| File.Exists ("cptgt" </> "samplefile")

[<Test>]
let ``copies folder flatten``() =
    ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

    do xake TestOptions {
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
let ``copies folder no flatten``() =
    ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

    do xake TestOptions {
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
let ``copies fileset NO flatten``() =
    ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

    do xake TestOptions {
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
let ``copies fileset flatten``() =
    ["cptgt"; "cpin"] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete (d, true))

    do xake TestOptions {
        rules [
            "main" => action {
                do! need ["cpin/a/samplefile"]
                do! cp {files !!"cpin/**/*"; todir "cptgt"; flatten}
            }

            "cpin/a/samplefile" ..> writeText "hello world"
        ]
    }

    assertTrue <| File.Exists ("cptgt" </> "samplefile")
