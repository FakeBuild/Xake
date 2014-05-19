namespace XakeLibTests

open System
open System.IO
open NUnit.Framework

open Xake
open Xake.BuildLog
open Xake.Storage

type Bookmark =
  | Bookmark of string * string
  | Folder of string * Bookmark list

[<TestFixture (Description = "Verious tests")>]
type StorageTests() =

  let dbname = "." </> ".xake"

  // stores object to a binary stream and immediately reads it
  let writeAndRead (pu:Pickler.PU<_>) testee =
    use buffer = new MemoryStream()
    pu.pickle testee (BinaryWriter buffer)
    buffer.Position <- 0L
    buffer.ToArray(), pu.unpickle (BinaryReader buffer)

  let createStrLogger (errorlist:System.Collections.Generic.List<string>) =
    CustomLogger (fun _ -> true) errorlist.Add

  let logger = ConsoleLogger Verbosity.Diag

  let createResult name =
    {(makeResult <| FileTarget (Artifact name)) with
      Depends = [File <| FileTarget (Artifact "abc.c"); Var ("DEBUG", "false")]
      Steps = [StepInfo ("compile", 217<ms>)]
    }

  let (<-*) (a:Agent<DatabaseApi>) t = a.PostAndReply(fun ch -> GetResult (t,ch))

  // Is.Any predicate for assertions
  let IsAny() = Is.Not.All.Not

  [<SetUp>]
  member this.Setup() =
    // delete database file
    try File.Delete(dbname) with _ -> ()

  [<Test (Description = "Verifies persisting of recursive types")>]
  [<Ignore ("Still no luck due to recursive types are not allowed in ML")>]
  member test.RecursiveType() =

    let wrapL (d:'a -> 'b, r: 'b -> 'a) (pu: Lazy<Pickler.PU<'a>>) = {Pickler.PU.pickle = r >> pu.Value.pickle; Pickler.PU.unpickle = pu.Value.unpickle >> d}

    let rec bookmarkPU =
      Pickler.alt
        (function | Bookmark _ -> 0 | Folder _ -> 1)
        [|
          wrapL (Bookmark, fun (Bookmark (name,url)) -> name,url) (lazy Pickler.pair Pickler.str Pickler.str)
          wrapL (Folder, fun (Folder (name,ls)) -> name,ls) (Lazy.Create (fun () -> Pickler.pair Pickler.str (bpu())))
        |]
    and bpu() = Pickler.list bookmarkPU

    let testee =
      Folder ("root",
        [
          Bookmark ("iXBT","http://ixbt.com");
          Bookmark ("fcenter","http://fcenter.ru");
          Folder ("work",
            [
              Bookmark ("fsharp.org","http://fsharp.org");
              Bookmark ("xamarin","http://xamarin.org")              
            ]);
          Folder ("empty", [])
        ])
    let buffer = MemoryStream()

    bookmarkPU.pickle testee (BinaryWriter buffer)

    buffer.Position <- 0L
    let replica = bookmarkPU.unpickle (BinaryReader buffer)

    Assert.AreEqual (testee, replica)

  [<Test (Description = "Verifies persisting simple data")>]
  member test.WriteBuildData() =
    let testee = makeResult <| FileTarget (Artifact "abc.exe")
    let testee =
      {testee with
        Depends = [
                    File <| FileTarget (Artifact "abc.c")
                    File <| FileTarget (Artifact "common.c")
                    EnvVar ("SDK", "4.5")
                    Var ("DEBUG", "false")
                  ]
        Steps = [
                  StepInfo ("preprocess", 187<ms>)
                  StepInfo ("compile", 217<ms>)
                  StepInfo ("link", 471<ms>)
                ]
      }
    
    let (buf,repl) = writeAndRead Storage.resultPU testee

    printfn "size is %A" buf.Length
    printfn "src %A" testee
    printfn "repl %A" repl
    Assert.AreEqual (testee, repl)

  [<Test (Description = "Verifies persisting simple data")>]
  member test.WriteReadDb() =

    let inline (<--) (agent: ^a) (msg: 'b) = (^a: (member Post: 'b -> unit) (agent, msg)); agent

    use testee = Storage.openDb "." logger
    testee
      <-- Store (createResult "abc.exe")
      <-- Store (createResult "def.exe")
      <-- Store (createResult "fgh.exe")
      |> ignore

    testee.PostAndReply CloseWait

    use testee = Storage.openDb "." logger

    let abc = testee <-* (FileTarget <| Artifact "abc.exe")
    Assert.IsTrue(Option.isSome abc)

    let def = testee <-* (FileTarget <| Artifact "def.exe")
    Assert.IsTrue(Option.isSome def)

    let fgh = testee <-* (FileTarget <| Artifact "fgh.exe")
    Assert.IsTrue(Option.isSome fgh)

    printfn "%A" abc
    testee.PostAndReply CloseWait

  [<Test (Description = "Verifies database self-compress")>]
  member test.DatabaseClean() =

    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs

    let inline (<--) (agent: ^a) (msg: 'b) = (^a: (member Post: 'b -> unit) (agent, msg)); agent

    use testee = Storage.openDb "." logger

    for j in seq {1..20} do
      for i in seq {1..20} do
        let name = sprintf "a%A.exe" i
        testee <-- Store (createResult name) |> ignore

    testee.PostAndReply CloseWait
    
    let oldLen = (FileInfo dbname).Length

    use testee = Storage.openDb "." logger
    testee.PostAndReply CloseWait

    let newLen = (FileInfo dbname).Length
    printfn "old size: %A, new size: %A" oldLen newLen

    Assert.Less((int newLen), (int oldLen)/3)
    Assert.That(msgs, IsAny().Contains("Compacting database"))

  [<Test (Description = "Verifies database update")>]
  member test.DatabaseUpdate() =

    let inline (<--) (agent: ^a) (msg: 'b) = (^a: (member Post: 'b -> unit) (agent, msg)); agent

    use testee = Storage.openDb "." logger

    let result = createResult "abc"
    testee <-- Store result |> ignore

    let updatedResult = {result with Depends = [Var ("DEBUG", "true")] }
    testee <-- Store updatedResult |> ignore

    testee.PostAndReply CloseWait
    
    use testee = Storage.openDb "." logger
    let (Some read) = testee <-* (FileTarget <| Artifact "abc")
    testee.PostAndReply CloseWait

    Assert.AreEqual ([Var ("DEBUG", "true")], read.Depends)

  [<Test (Description = "Verifies database restoration from backup")>]
  member test.RestoreDb() =

    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs
    let inline (<--) (agent: ^a) (msg: 'b) = (^a: (member Post: 'b -> unit) (agent, msg)); agent

    use testee = Storage.openDb "." logger
    testee <-- Store (createResult "abc") |> ignore
    testee.PostAndReply CloseWait

    let bkdb = "." </> ".xake" <.> "bak"
    File.Move (dbname, bkdb)

    File.WriteAllText (dbname, "dummy text")
    
    use testee = Storage.openDb "." logger
    let read = testee <-* (FileTarget <| Artifact "abc")
    Assert.IsTrue(Option.isSome read)
    testee.PostAndReply CloseWait

    Assert.That(msgs, IsAny().Contains("restoring db"))

  [<Test (Description = "Verifies broken db will be repaired")>]
  member test.CleanBrokenDb() =

    let msgs = System.Collections.Generic.List<string>()
    let logger = createStrLogger msgs
    let inline (<--) (agent: ^a) (msg: 'b) = (^a: (member Post: 'b -> unit) (agent, msg)); agent

    File.WriteAllText (dbname, "dummy text")

    use testee = Storage.openDb "." logger
    testee <-- Store (createResult "abc") |> ignore
    let read = testee <-* (FileTarget <| Artifact "abc")
    Assert.IsTrue(Option.isSome read)
    testee.PostAndReply CloseWait

    Assert.That(msgs, IsAny().Contains("Failed to read database"))

