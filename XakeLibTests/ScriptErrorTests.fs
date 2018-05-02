namespace XakeLibTests

open System.IO
open System.Collections.Generic
open NUnit.Framework

open Xake
open Xake.Tasks

[<TestFixture>]
type ``Script error handling``() =

  let rememberDir = Directory.GetCurrentDirectory()
  let outFolder = rememberDir </> "~testdata~" </> "scripterr"

  let makeDebugOptions (errorlist:List<string>) =
    {ExecOptions.Default with FailOnError = true; CustomLogger = CustomLogger ((=) Level.Error) errorlist.Add; FileLog = ""; ProjectRoot = outFolder}

  [<OneTimeSetUp>]
  member __.OneTimeSetup () =
      Directory.CreateDirectory outFolder |> ignore
      Directory.SetCurrentDirectory outFolder

  [<OneTimeTearDown>]
  member __.Teardown () =
      Directory.SetCurrentDirectory rememberDir

  [<SetUp>]
  member __.Setup() =
      try File.Delete("." </> ".xake") with _ -> ()

  [<Test>]
  member __.``verifies executing target action``() =

    let wasExecuted = ref false
    
    do xake ExecOptions.Default {
      want (["test"])
      phony "test" (action {
        do! trace Info "Running inside 'test' rule"
        wasExecuted := true
      })
    }

    Assert.IsTrue(!wasExecuted)

  [<Test>]
  member __.``handles (throws exception) exception is thrown in script body``() =

    let errorlist = new System.Collections.Generic.List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (makeDebugOptions errorlist) {
        want (["test"])
        phony "test" (action {
          do! trace Info "Running inside 'test' rule"
          failwith "exception happens"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

  [<Test>]
  member __.``handles exception occured in inner rule``() =

    let errorlist = new System.Collections.Generic.List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (makeDebugOptions errorlist) {
        want (["test"])
        phony "test" (action {
          do! trace Info "Running inside 'test' rule"
          do! need ["clean"]
        })
        phony "clean" (action {
          do! trace Info "Running inside 'clean' rule"
          failwith "exception happens"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("exception happens")))

  [<Test>]
  member __.``fails if rule is not found``() =

    let errorlist = new List<string>()

    Assert.Throws<XakeException> (fun () ->
      do xake (makeDebugOptions errorlist) {
        want (["test"])
        phony "clean" (action {
          do! trace Info "Running inside 'clean' rule"
        })
      }) |> ignore

    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Neither rule nor file is found")))

  [<Test>]
  member __.``handles broken database``() =

    let dbname = "." </> ".xake"
    File.WriteAllText (dbname, "dummy text")
    let errorlist = new List<string>()

    Assert.DoesNotThrow (fun () ->
        do xake (makeDebugOptions errorlist) {
            want (["clean"; "make"])
            phony "clean" (recipe {
              do! rm {file "*.failbroken"}
              do! trace Info "Running inside 'clean' rule"
            })
            phony "make" (recipe {
              File.WriteAllText ("1.failbroken", "abc")
              File.WriteAllText ("2.failbroken", "def")
              do! trace Info "Running inside 'build' rule"
            })
          }
        ) |> ignore
    printf "result is %A" errorlist
    Assert.IsTrue(errorlist.Exists (fun (x:string) -> x.Contains("Failed to read database, so recreating")))
