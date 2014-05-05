namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake

[<TestFixture (Description = "Unit tests for error handling")>]
type ScriptErrorTests() =

  let DebugOptions = XakeOptions  // TODO set inner state, loggers etc

  [<Test (Description = "Verifies ls function")>]
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

  [<Test (Description = "Verifies the script properly fails if exception is thrown internally")>]
  member test.FailsOnInnerException() =

    let errorlist = []

    do xake XakeOptions {
      want (["test"])
      phony "test" (action {
        do! writeLog Info "Running inside 'test' rule"
        failwith "exception happens"
      })
    }

    Assert.IsTrue(errorlist |> List.exists (fun (x:string) -> x.Contains("exception happens")))
    Assert.IsTrue(errorlist |> List.exists (fun (x:string) -> x.Contains("script execution failed")))

    // TODO option to bypass the exceptions
