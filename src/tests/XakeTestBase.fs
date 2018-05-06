namespace Tests

open System.IO
open NUnit.Framework

open Xake

type XakeTestBase(folder) =

    let mutable rememberDir = ""
    let mutable outFolder = ""

    let mutable testOptions = ExecOptions.Default
    
    member x.TestOptions with get() = testOptions

    [<OneTimeSetUp>]
    member __.Setup () =
        rememberDir <- Directory.GetCurrentDirectory()
        outFolder <- rememberDir </> "~testout~" </> folder
        testOptions <- {
            ExecOptions.Default with
                Threads = 1
                Targets = ["main"]
                ConLogLevel = Diag; FileLogLevel = Silent
                ProjectRoot = outFolder
        }

        Directory.CreateDirectory outFolder |> ignore
        Directory.SetCurrentDirectory outFolder

    [<OneTimeTearDown>]
    member __.Teardown () =
        Directory.SetCurrentDirectory rememberDir

    [<SetUp>]
    member __.SetupTest () =
        "." </> ".xake" |> File.Delete