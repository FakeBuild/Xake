namespace XakeLibTests

open System.IO
open System.Collections.Generic
open NUnit.Framework

open Xake

[<TestFixture>]
type ``action block allows``() =

  let MakeDebugOptions() =
    let errorlist = new System.Collections.Generic.List<string>() in
    {XakeOptions with FailOnError = true; CustomLogger = CustomLogger ((=) Level.Error) errorlist.Add; FileLog = ""},errorlist

  let DebugOptions =
    {XakeOptions with FailOnError = true; FileLog = ""}

  [<Test (Description = "Verifies we could use logging from inside action block")>]
  member test.Simple() =

    let wasExecuted = ref false
    
    do xake DebugOptions {
      phony "main" (action {
        wasExecuted := true
      })
    }

    Assert.IsTrue(!wasExecuted)

  [<Test (Description = "Verifies simple statements")>]
  member test.Logging() =

    let wasExecuted = ref false

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add
    
    do xake DebugOptions {
      phony "main" (action {
        do note "1"
        wasExecuted := true
        do note "2"
      })
    }

    do note "3"

    Assert.IsTrue(!wasExecuted)
    Assert.That(errorlist, Is.EquivalentTo(["1"; "2"; "3"]))

  [<Test (Description = "Verifies do!")>]
  member test.``do! async op``() =

    let wasExecuted = ref false

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    do xake DebugOptions {
      phony "main" (action {
        do! async {note "1"}
        wasExecuted := true
        do! async {note "2"}
        do note "3"
      })
    }

    do note "4"

    Assert.IsTrue(!wasExecuted)
    Assert.That(errorlist, Is.EqualTo(["1"; "2"; "3"; "4"]))

  [<Test>]
  member test.``do! action``() =

    let wasExecuted = ref false

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    let noteAction t = action {do note t}
    
    do xake DebugOptions {
      phony "main" (action {
        do! noteAction "1"
        wasExecuted := true
        do! noteAction "2"
        do note "3"
      })
    }

    do note "4"

    Assert.IsTrue(!wasExecuted)
    Assert.That(errorlist, Is.EqualTo(["1"; "2"; "3"; "4"]))


  [<Test>]
  member test.``do! action with result ignored``() =

    let wasExecuted = ref false

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    let testee t =
        action {
            do note t
            return t
        }
    
    do xake DebugOptions {
      phony "main" (action {
        do! (testee "1") |> Action.Ignore
        wasExecuted := true
        do! testee "2" |> Action.Ignore
        do note "3"
      })
    }

    do note "4"

    Assert.IsTrue(!wasExecuted)
    Assert.That(errorlist, Is.EqualTo(["1"; "2"; "3"; "4"] |> List.toArray))


  [<Test>]
  member test.``let! returning value``() =

    let wasExecuted = ref false

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    let testee t =
        action {
            return t
        }
    
    do xake DebugOptions {
      phony "main" (action {
        let! s1 = testee "1"
        do note s1
        wasExecuted := true
        let! s2 = testee ("2+" + s1)
        do note s2
        do note "3"
      })
    }

    do note "4"

    Assert.IsTrue(!wasExecuted)
    Assert.That(errorlist, Is.EqualTo(["1"; "2+1"; "3"; "4"] |> List.toArray))
