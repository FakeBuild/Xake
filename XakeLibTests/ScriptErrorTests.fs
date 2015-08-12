namespace XakeLibTests

open System.IO
open System.Collections.Generic
open NUnit.Framework

open Xake

[<TestFixture>]
type ``Script error handling``() =

  let MakeDebugOptions (errorlist:List<string>) =
    {ExecOptions.Default with FailOnError = true; CustomLogger = CustomLogger ((=) Level.Error) errorlist.Add; FileLog = ""}

  [<SetUp>]
  member test.Setup() =
      try File.Delete("." </> ".xake") with _ -> ()

  [<Test>]
  member test.``verifies executing target action``() =

    let wasExecuted = ref false
    
    do xake ExecOptions.Default {
      want (["test"])
      phony "test" (action {
        do! writeLog Info "Running inside 'test' rule"
        wasExecuted := true
      })
    }

    Assert.IsTrue(!wasExecuted)

  [<Test>]
  member test.``handles (throws exception) exception is thrown in script body``() =

    let errorlist = new System.Collections.Generic.List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (MakeDebugOptions errorlist) {
        want (["test"])
        phony "test" (action {
          do! writeLog Info "Running inside 'test' rule"
          failwith "exception happens"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

  [<Test>]
  member test.``handles exception occured in inner rule``() =

    let errorlist = new System.Collections.Generic.List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (MakeDebugOptions errorlist) {
        want (["test"])
        phony "test" (action {
          do! writeLog Info "Running inside 'test' rule"
          do! need ["clean"]
        })
        phony "clean" (action {
          do! writeLog Info "Running inside 'clean' rule"
          failwith "exception happens"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

  [<Test>]
  member test.``fails if rule is not found``() =

    let errorlist = new List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (MakeDebugOptions errorlist) {
        want (["test"])
        phony "clean" (action {
          do! writeLog Info "Running inside 'clean' rule"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Neither rule nor file is found")))

  [<Test>]
  member test.``handles broken database``() =

    let dbname = "." </> ".xake"
    File.WriteAllText (dbname, "dummy text")
    let errorlist = new List<string>()

    Assert.DoesNotThrow (fun () ->
        do xake (MakeDebugOptions errorlist) {
            want (["clean"; "make"])
            phony "clean" (action {
              do! rm ["*.failbroken"]
              do! writeLog Info "Running inside 'clean' rule"
            })
            phony "make" (action {
              File.WriteAllText ("1.failbroken", "abc")
              File.WriteAllText ("2.failbroken", "def")
              do! writeLog Info "Running inside 'build' rule"
            })
          }
        ) |> ignore
    printf "result is %A" errorlist
    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Failed to read database, so recreating")))
