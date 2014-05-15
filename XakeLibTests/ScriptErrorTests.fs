namespace XakeLibTests

open System.IO
open System.Collections.Generic
open NUnit.Framework

open Xake

[<TestFixture (Description = "Unit tests for error handling")>]
type ScriptErrorTests() =

  let MakeDebugOptions (errorlist:List<string>) = //XakeOptions  // TODO set inner state, loggers etc
    {XakeOptions with FailOnError = true; CustomLogger = CustomLogger ((=) Level.Error) errorlist.Add; FileLog = ""}

  [<Test (Description = "Verifies executing target action")>]
  member test.GoodRun() =

    let wasExecuted = ref false
    
    do xake XakeOptions {
      want (["test"])
      phony "test" (action {
        do! writeLog Info "Running inside 'test' rule"
        wasExecuted := true
      })
    }

    Assert.IsTrue(!wasExecuted)

  [<Test (Description = "Verifies the script properly fails if exception is thrown in rule body")>]
  member test.FailsOnExceptionInRule() =

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

  [<Test (Description = "Verifies the script properly fails if exception is thrown internally")>]
  member test.FailsOnExceptionInDependentRule() =

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

  [<Test (Description = "Verifies the script properly fails if rule does not exists")>]
  member test.FailsOnNonExistingRule() =

    let errorlist = new List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (MakeDebugOptions errorlist) {
        want (["test"])
        phony "clean" (action {
          do! writeLog Info "Running inside 'clean' rule"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Neither rule nor file is found")))