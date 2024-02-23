module ``action block is capable of``

open NUnit.Framework
open Xake

let makeStringList() = new System.Collections.Generic.List<string>()
let DebugOptions = {ExecOptions.Default with ThrowOnError = true; FileLog = ""}

[<Test>]
let ``execute the body``() =

    let wasExecuted = ref false
    
    do xake DebugOptions {
      phony "main" (recipe {
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
      phony "main" (recipe {
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
      phony "main" (recipe {
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

    let noteAction t = recipe {do note t}
    
    do xake DebugOptions {
      phony "main" (recipe {
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
        recipe {
            do note t
            return t
        }
    
    do xake DebugOptions {
        phony "main" (recipe {
            do! (testee "1") |> Ignore
            wasExecuted := true
            do! testee "2" |> Recipe.Ignore
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
        recipe {
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
        recipe {
            return if f then a else b
        }
    
    do xake DebugOptions {
      phony "main" (recipe {
        if true then
            do note "i1-t"
        else
            do note "i1-f"

        if false then
            ()
        else
            do note "i2-f"

        let! s1 = iif true "2" "2f"
        let! s1 = if s1 = "2" then iif true "2" "2f" else recipe {return "2"}
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
        recipe {
            return if f then a else b
        }
    
    do xake DebugOptions {

      phony "main" (recipe {
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

      phony "main" (recipe {

        let! s1 = recipe {return "122"}
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
    let anote txt = recipe {
        do note txt
    }

    do xake DebugOptions {

      phony "main" (recipe {
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
    let anote txt = recipe {
        do errorlist.Add txt
    }

    do xake DebugOptions {

      phony "main" (recipe {
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
    let anote txt = recipe {
        do errorlist.Add txt
    }

    do xake DebugOptions {

      phony "main" (recipe {
        do! anote "before try"

        try
            printfn "Body executed"
            do! anote "try"
            failwith "Ouch"
        with e ->
            do! anote e.Message
        
        do! anote "4"
      })
    } 

    // printfn "%A" errorlist
    Assert.That(errorlist, Is.EqualTo(["before try"; "try"; "Ouch"; "4"] |> List.toArray))

[<Test>]
let ``WhenError function to handle exceptions within actions``() =

    let taskReturn n = recipe {
        return n
    }

    let excCount = ref 0
    do xake DebugOptions {
        rules [
            "main" => (
                WhenError (fun _ -> excCount := 1) <|
                recipe {
                    printfn "Some useful job"
                    do! taskReturn 3 |> FailWhen ((=) 3) "err" |> Recipe.Ignore
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
            recipe {
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

type DisposeMock(count) =
    member this.DoNothing() =
        printfn "doing nothing"
    interface System.IDisposable with 
        member this.Dispose() = 
            count := !count + 1
            printfn "disposed"

[<Test; Explicit>]
let ``use! for disposable resources``() =
    let excCount = ref 0
    let disposeCount = ref 0
    do xake DebugOptions {
        rules [
            "main" =>
            recipe {
                try
                    printfn "Some useful job"
                    use a = new DisposeMock(disposeCount)
                    printfn "inside use"
                    a.DoNothing()
                    do 3/0 |> ignore
                with _ ->
                    excCount := 1
                    printfn "caught exception"
                printfn "after trywith"
            }
        ]
    }

    Assert.AreEqual(1, !excCount)
    Assert.AreEqual(1, !disposeCount)

