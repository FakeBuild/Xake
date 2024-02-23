namespace Tests

open System.IO
open System.Collections.Generic
open NUnit.Framework

open Xake
open Xake.Tasks

[<TestFixture>]
type ``Script error handling``() =
    inherit XakeTestBase("scripterr")

    member x.MakeDebugOptions (errorlist:List<string>) =
        {x.TestOptions with ThrowOnError = true; CustomLogger = CustomLogger ((=) Level.Error) errorlist.Add; FileLog = ""}

    [<Test>]
    member __.``verifies executing target action``() =

        let wasExecuted = ref false
        
        do xake ExecOptions.Default {
            want (["test"])
            phony "test" (recipe {
                do! trace Info "Running inside 'test' rule"
                wasExecuted := true
            })
        }

        Assert.IsTrue(!wasExecuted)

    [<Test>]
    member x.``handles (throws exception) exception is thrown in script body``() =

        let errorlist = new System.Collections.Generic.List<string>()

        Assert.Throws<XakeException> (fun () ->
          do xake (x.MakeDebugOptions errorlist) {
            phony "main" (recipe {
              do! trace Info "Running inside 'test' rule"
              failwith "exception happens"
            })
          }) |> ignore

        Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

    [<Test>]
    member x.``handles exception occured in inner rule``() =

        let errorlist = new System.Collections.Generic.List<string>()

        Assert.Throws<XakeException> (fun () ->
            do xake (x.MakeDebugOptions errorlist) {
                phony "main" (recipe {
                    do! trace Info "Running inside 'test' rule"
                    do! need ["clean"]
                })
                phony "clean" (recipe {
                    do! trace Info "Running inside 'clean' rule"
                    failwith "exception happens"
                })
            }) |> ignore

        Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

    [<Test>]
    member x.``fails if rule is not found``() =

        let errorlist = new List<string>()

        Assert.Throws<XakeException> (fun () ->
            do xake (x.MakeDebugOptions errorlist) {
                want (["test"])
                phony "clean" (recipe {
                   do! trace Info "Running inside 'clean' rule"
                })
            }) |> ignore

        Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Neither rule nor file is found")))

    [<Test>]
    member x.``handles broken database``() =

        let dbname = "." </> ".xake"
        File.WriteAllText (dbname, "dummy text")
        let errorlist = new List<string>()

        Assert.DoesNotThrow (fun () ->
            do xake (x.MakeDebugOptions errorlist) {
                rules [
                    "main" <<< ["clean"; "make"]
                    "clean" => recipe {
                        do! rm {file "*.failbroken"}
                        do! trace Info "Running inside 'clean' rule"
                    }
                    "make" => recipe {
                        File.WriteAllText ("1.failbroken", "abc")
                        File.WriteAllText ("2.failbroken", "def")
                        do! trace Info "Running inside 'build' rule"
                    }
                ]
              }
            ) |> ignore
        printf "result is %A" errorlist
        Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Failed to read database, so recreating")))
