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
        let! s2 = testee ("2+" + s1)
        do note s2
        do note "3"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["1"; "2+1"; "3"] |> List.toArray))

  [<Test>]
  member test.``if of various kinds``() =

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    let iif f a b =
        action {
            return if f then a else b
        }
    
    do xake DebugOptions {
      phony "main" (action {
        if true then
            do note "i1-t"
        else
            do note "i1-f"

        if false then
            ()
        else
            do note "i2-f"

        let! s1 = iif true "2" "2f"
        let! s1 = if s1 = "2" then iif true "2" "2f" else action {return "2"}
        do note s1
        do note "3"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["i1-t"; "i2-f"; "2"; "3"] |> List.toArray))

  [<Test>]
  member test.``if without else``() =

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    let iif f a b =
        action {
            return if f then a else b
        }
    
    do xake DebugOptions {

      phony "main" (action {
        if true then
            do note "i1-t"

        let! s1 = iif true "2" "2f"
        if s1 = "2" then
            do note "3"

        for i in [1..5] do
            ()
        
        do note "4"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["i1-t"; "3"; "4"] |> List.toArray))

  [<Test>]
  member test.``for and while``() =

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    do xake DebugOptions {

      phony "main" (action {

        let! s1 = action {return "122"}
        let s2 = s1
        do note "1"

        for i in [1..3] do
            do! writeLog Info "%A" i
            do note (sprintf "i=%i" i)

        let j = ref 3
        while !j < 5 do
            do note (sprintf "j=%i" !j)
            j := !j + 1
        
        do note "4"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["1"; "i=1"; "i=2"; "i=3"; "j=3"; "j=4"; "4"] |> List.toArray))

  [<Test; Explicit>]
  member test.``try catch finally``() =

    let errorlist = new System.Collections.Generic.List<string>()
    let note = errorlist.Add

    do xake DebugOptions {

      phony "main" (action {

        let! s1 = action {return "122"}
        do note s1

        note "before try"

//        try
//           printfn "Body executed"
//           do note "try"
//        finally
//           printfn "Finally executed"
//           do note "finally"

//        try
//            failwith "ee"
//        with e ->
//            do note e.Message
        
        do note "4"
      })
    }

    // "2222"; "ee"; 
    printfn "%A" errorlist
    Assert.That(errorlist, Is.EqualTo(["122"; "try"; "finally"; "4"] |> List.toArray))

// TODO use!, try with exception within action

