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

[<Test>]
let ``persists simple data``() =

    let testee = makeResult <| (mkFileTarget "abc.exe")

    let testee =
        { testee with
            Depends =
              [
                ArtifactDep <| (mkFileTarget "abc.c")
                FileDep (File.make "common.c", System.DateTime(1971, 11, 21))
                EnvVar("SDK", Some "4.5")
                Var("DEBUG", Some "false") ]
            Steps =
              [
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

    use testee = Storage.openDb dbname logger
    testee <-- Store(createResult "abc.exe") <-- Store(createResult "def.exe") <-- Store(createResult "fgh.exe")
    |> ignore
    testee.PostAndReply CloseWait
    use testee = Storage.openDb dbname logger
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

    use testee = Storage.openDb dbname logger
    for j in seq { 1..20 } do
        for i in seq { 1..20 } do
            let name = sprintf "a%A.exe" i
            testee <-- Store(createResult name) |> ignore
    testee.PostAndReply CloseWait
    let oldLen = (FileInfo dbname).Length
    use testee = Storage.openDb dbname logger
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

    use testee = Storage.openDb dbname logger
    let result = createResult "abc"
    testee <-- Store result |> ignore
    let updatedResult = { result with Depends = [ Var("DEBUG", Some "true") ] }
    testee <-- Store updatedResult |> ignore
    testee.PostAndReply CloseWait
    use testee = Storage.openDb dbname logger
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

    use testee = Storage.openDb dbname logger
    testee <-- Store(createResult "abc") |> ignore
    testee.PostAndReply CloseWait
    let bkdb = "." </> ".xake" <.> "bak"
    File.Move(dbname, bkdb)
    File.WriteAllText(dbname, "dummy text")
    use testee = Storage.openDb dbname logger
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
    use testee = Storage.openDb dbname logger
    testee <-- Store(createResult "abc") |> ignore
    let read = testee <-* mkFileTarget "abc"
    Assert.IsTrue(Option.isSome read)
    testee.PostAndReply CloseWait
    Assert.That(msgs, IsAny().Contains("Failed to read database"))
