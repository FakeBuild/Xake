module ``Storage facility``

open System.IO
open NUnit.Framework

open Xake
open Xake.BuildLog
open Xake.Storage

type Bookmark =
    | Bookmark of string * string
    | Folder of string * Bookmark list

module private impl =
    let dbname = "." </> ".xake"

    let mkFileTarget = File.make >> FileTarget
    let newStepInfo (name, duration) =
        { StepInfo.Empty with Name = name
                              OwnTime = duration * 1<ms> }

    // stores object to a binary stream and immediately reads it
    let writeAndRead (pu : Pickler.PU<_>) testee =
        use buffer = new MemoryStream()
        pu.pickle testee (new BinaryWriter(buffer))
        buffer.Position <- 0L
        buffer.ToArray(), pu.unpickle (new BinaryReader(buffer))

    let createStrLogger (errorlist : System.Collections.Generic.List<string>) =
        CustomLogger (fun _ -> true) errorlist.Add
    let logger = ConsoleLogger Verbosity.Diag

    let createResult name =
        { (name
           |> File.make
           |> FileTarget
           |> makeResult) with Depends =
                                   [ "abc.c" |> mkFileTarget |> ArtifactDep
                                     Var("DEBUG", Some "false") ]
                               Steps = [ newStepInfo ("compile", 217) ] }
    
    let (<-*) (a : Agent<DatabaseApi>) t = a.PostAndReply(fun ch -> GetResult(t, ch))
    // Is.Any predicate for assertions
    let IsAny() = Is.Not.All.Not

open impl

[<SetUp>]
let Setup() =
    // delete database file
    try
        File.Delete(dbname)
    with _ -> ()

[<Test(Description = "Verifies persisting of recursive types")>]
[<Ignore("Still no luck due to recursive types are not allowed in ML")>]
let RecursiveType() =
    let wrapL (d : 'a -> 'b, r : 'b -> 'a) (pu : Lazy<Pickler.PU<'a>>) =
        { Pickler.PU.pickle = r >> pu.Value.pickle
          Pickler.PU.unpickle = pu.Value.unpickle >> d }

    let rec bookmarkPU =
        Pickler.alt (function
            | Bookmark _ -> 0
            | Folder _ -> 1)
            [| wrapL (Bookmark, fun (Bookmark(name, url)) -> name, url) (lazy Pickler.pair Pickler.str Pickler.str)
               wrapL (Folder, fun (Folder(name, ls)) -> name, ls)
                    (Lazy.Create(fun () -> Pickler.pair Pickler.str (bpu()))) |]

    and bpu() = Pickler.list bookmarkPU

    let testee =
        Folder("root",
                [ Bookmark("iXBT", "http://ixbt.com")
                  Bookmark("fcenter", "http://fcenter.ru")
                  Folder("work",
                        [ Bookmark("fsharp.org", "http://fsharp.org")
                          Bookmark("xamarin", "http://xamarin.org") ])
                  Folder("empty", []) ])

    let buffer = new MemoryStream()
    bookmarkPU.pickle testee (new BinaryWriter(buffer))
    buffer.Position <- 0L
    let replica = bookmarkPU.unpickle (new BinaryReader(buffer))
    Assert.AreEqual(testee, replica)


[<Test>]
let ``persists simple data``() =

    let testee = makeResult <| (mkFileTarget "abc.exe")

    let testee =
        { testee with
            Depends = [
                ArtifactDep <| (mkFileTarget "abc.c")
                FileDep (File.make "common.c", System.DateTime(1971, 11, 21))
                EnvVar("SDK", Some "4.5")
                Var("DEBUG", Some "false") ]
            Steps = [
                newStepInfo ("preprocess", 187)
                newStepInfo ("compile", 217)
                newStepInfo ("link", 471) ] }

    let (buf, repl) = writeAndRead Storage.resultPU testee
    printfn "size is %A" buf.Length
    printfn "src %A" testee
    printfn "repl %A" repl
    Assert.AreEqual(testee, repl)

[<Test>]
let ``persists build data in Xake db``() =
    let inline (<--) (agent : ^a) (msg : 'b) =
        (^a : (member Post : 'b -> unit) (agent, msg))
        agent

    use testee = Storage.openDb "." logger
    testee <-- Store(createResult "abc.exe") <-- Store(createResult "def.exe") <-- Store(createResult "fgh.exe")
    |> ignore
    testee.PostAndReply CloseWait
    use testee = Storage.openDb "." logger
    let abc = testee <-* (mkFileTarget "abc.exe")
    Assert.IsTrue(Option.isSome abc)
    let def = testee <-* (mkFileTarget "def.exe")
    Assert.IsTrue(Option.isSome def)
    let fgh = testee <-* (mkFileTarget "fgh.exe")
    Assert.IsTrue(Option.isSome fgh)
    printfn "%A" abc
    testee.PostAndReply CloseWait

[<Test>]
let ``compresses database when limit is reached``() =
    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs

    let inline (<--) (agent : ^a) (msg : 'b) =
        (^a : (member Post : 'b -> unit) (agent, msg))
        agent

    use testee = Storage.openDb "." logger
    for j in seq { 1..20 } do
        for i in seq { 1..20 } do
            let name = sprintf "a%A.exe" i
            testee <-- Store(createResult name) |> ignore
    testee.PostAndReply CloseWait
    let oldLen = (FileInfo dbname).Length
    use testee = Storage.openDb "." logger
    testee.PostAndReply CloseWait
    let newLen = (FileInfo dbname).Length
    printfn "old size: %A, new size: %A" oldLen newLen
    Assert.Less((int newLen), (int oldLen) / 3)
    Assert.That(msgs, IsAny().Contains("Compacting database"))

[<Test>]
let ``updates data in file storage``() =
    let inline (<--) (agent : ^a) (msg : 'b) =
        (^a : (member Post : 'b -> unit) (agent, msg))
        agent

    use testee = Storage.openDb "." logger
    let result = createResult "abc"
    testee <-- Store result |> ignore
    let updatedResult = { result with Depends = [ Var("DEBUG", Some "true") ] }
    testee <-- Store updatedResult |> ignore
    testee.PostAndReply CloseWait
    use testee = Storage.openDb "." logger
    let (Some read) = testee <-* (mkFileTarget "abc")
    testee.PostAndReply CloseWait
    Assert.AreEqual([ Var("DEBUG", Some "true") ], read.Depends)

[<Test>]
let ``restores db in case write failed``() =
    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs

    let inline (<--) (agent : ^a) (msg : 'b) =
        (^a : (member Post : 'b -> unit) (agent, msg))
        agent

    use testee = Storage.openDb "." logger
    testee <-- Store(createResult "abc") |> ignore
    testee.PostAndReply CloseWait
    let bkdb = "." </> ".xake" <.> "bak"
    File.Move(dbname, bkdb)
    File.WriteAllText(dbname, "dummy text")
    use testee = Storage.openDb "." logger
    let read = testee <-* (mkFileTarget "abc")
    Assert.IsTrue(Option.isSome read)
    testee.PostAndReply CloseWait
    Assert.That(msgs, IsAny().Contains("restoring db"))

[<Test>]
let ``repairs (cleans) broken db``() =
    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs

    let inline (<--) (agent : ^a) (msg : 'b) =
        (^a : (member Post : 'b -> unit) (agent, msg))
        agent
    File.WriteAllText(dbname, "dummy text")
    use testee = Storage.openDb "." logger
    testee <-- Store(createResult "abc") |> ignore
    let read = testee <-* mkFileTarget "abc"
    Assert.IsTrue(Option.isSome read)
    testee.PostAndReply CloseWait
    Assert.That(msgs, IsAny().Contains("Failed to read database"))
