module ``action block is capable of``

open NUnit.Framework
open Xake

let makeStringList() = new System.Collections.Generic.List<string>()
let DebugOptions = {ExecOptions.Default with FailOnError = true; FileLog = ""}

[<Test>]
let ``execute the body``() =

    let wasExecuted = ref false
    
    do xake DebugOptions {
      phony "main" (action {
        wasExecuted := true
      })
    }

    Assert.IsTrue(!wasExecuted)

[<Test>]
let ``ordered execution``() =

    let wasExecuted = ref false

    let errorlist = makeStringList()
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

[<Test>]
let ``starting async operations``() =

    let wasExecuted = ref false

    let errorlist = makeStringList()
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
let ``invoke other actions within action (do!)``() =

    let wasExecuted = ref false

    let errorlist = makeStringList()
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
let ``ignoring result of do! action``() =

    let wasExecuted = ref false

    let errorlist = makeStringList()
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
let ``obtaining action result``() =

    let errorlist = makeStringList()
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
let ``ifs within actions``() =

    let errorlist = makeStringList()
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
let ``branching using if without else``() =

    let errorlist = makeStringList()
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

        for _ in [1..5] do
            ()
        
        do note "4"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["i1-t"; "3"; "4"] |> List.toArray))

[<Test>]
let ``for and while loops``() =

    let errorlist = makeStringList()
    let note = errorlist.Add

    do xake DebugOptions {

      phony "main" (action {

        let! s1 = action {return "122"}
        let s2 = s1
        do note "1"

        for i in [1..3] do
            do! trace Info "%A" i
            do note (sprintf "i=%i" i)

        let j = ref 3
        while !j < 5 do
            do note (sprintf "j=%i" !j)
            j := !j + 1
        
        do note "4"
      })
    }

    Assert.That(errorlist, Is.EqualTo(["1"; "i=1"; "i=2"; "i=3"; "j=3"; "j=4"; "4"] |> List.toArray))

[<Test>]
let ``exception handling with 'try finally'``() =

    let errorlist = makeStringList()
    let note = errorlist.Add
    let anote txt = action {
        do note txt
    }

    do xake DebugOptions {

      phony "main" (action {
        note "before try"
        try
            printfn "Body executed"
            do! anote "try"
        finally
            printfn "Finally executed"
            do note "finally"
        do note "4"
      })
    }

    // printfn "%A" errorlist
    Assert.That(errorlist, Is.EqualTo(["before try"; "try"; "finally"; "4"] |> List.toArray))

[<Test>]
let ``try finally fail``() =

    let errorlist = makeStringList()
    let note = errorlist.Add
    let anote txt = action {
        do errorlist.Add txt
    }

    do xake DebugOptions {

      phony "main" (action {
        do! anote "before try"

        try
            printfn "Body executed"
            do! anote "try"
            failwith "Ouch"
        finally
            printfn "Finally executed"
            do note "finally"
        
        do! anote "4"
      } |> WhenError ignore)
    } 

    // printfn "%A" errorlist
    Assert.That(errorlist, Is.EqualTo(["before try"; "try"; "finally"] |> List.toArray))

[<Test>]
let ``exception handling with 'try with'``() =

    let errorlist = makeStringList()
    let note = errorlist.Add
    let anote txt = action {
        do errorlist.Add txt
    }

    do xake DebugOptions {

      phony "main" (action {
        do! anote "before try"

        try
            printfn "Body executed"
            do! anote "try"
            failwith "Ouch"
        with e ->
            printfn "Finally executed"
            do note e.Message
        
        do! anote "4"
      })
    } 

    // printfn "%A" errorlist
    Assert.That(errorlist, Is.EqualTo(["before try"; "try"; "Ouch"; "4"] |> List.toArray))

[<Test>]
let ``WhenError function to handle exceptions within actions``() =

    let taskReturn n = action {
        return n
    }

    let excCount = ref 0
    do xake DebugOptions {
        rules [
            "main" => (
                WhenError (fun _ -> excCount := 1) <|
                action {
                    printfn "Some useful job"
                    do! taskReturn 3 |> FailWhen ((=) 3) "err" |> Action.Ignore
                    printfn "This wont run"
                })
        ]
    }

    Assert.AreEqual(1, !excCount)

[<Test>]
let ``try/with for the whole script body``() =
    let excCount = ref 0
    do xake DebugOptions {
        rules [
            "main" =>
            action {
                try
                    printfn "Some useful job"
                    do 3/0 |> ignore
                    printfn "This wont run"
                with _ ->
                    excCount := 1
            }
        ]
    }

    Assert.AreEqual(1, !excCount)

// TODO use!, try with exception within action

