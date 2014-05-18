namespace XakeLibTests

open System
open System.IO
open NUnit.Framework

open Xake
open Xake.Pickler
open Xake.BuildLog
open Xake.Storage

type Bookmark =
  | Bookmark of string * string
  | Folder of string * Bookmark list

[<TestFixture (Description = "Verious tests")>]
type StorageTests() =

  // stores object to a binary stream and immediately reads it
  let writeAndRead pu testee =
    use buffer = new MemoryStream()
    pu.pickle testee (BinaryWriter buffer)
    buffer.Position <- 0L
    buffer.ToArray(), pu.unpickle (BinaryReader buffer)

  let createStrLogger (errorlist:System.Collections.Generic.List<string>) =
    CustomLogger (fun _ -> true) errorlist.Add

  [<Test (Description = "Verifies persisting of recursive types")>]
  [<Ignore ("Still no luck due to recursive types are not allowed in ML")>]
  member test.RecursiveType() =

    let wrapL (d:'a -> 'b, r: 'b -> 'a) (pu: Lazy<PU<'a>>) = {pickle = r >> pu.Value.pickle; unpickle = pu.Value.unpickle >> d}

    let rec bookmarkPU =
      altPU
        (function | Bookmark _ -> 0 | Folder _ -> 1)
        [|
          wrapL (Bookmark, fun (Bookmark (name,url)) -> name,url) (lazy pairPU strPU strPU)
          wrapL (Folder, fun (Folder (name,ls)) -> name,ls) (Lazy.Create (fun () -> pairPU strPU (bpu())))
        |]
    and bpu() = listPU bookmarkPU

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

//    let errorlist = new System.Collections.Generic.List<string>()
//    let logger = createStrLogger errorlist
    let logger = ConsoleLogger Verbosity.Diag

    let createResult name =
      {(makeResult <| FileTarget (Artifact name)) with
        Depends = [File <| FileTarget (Artifact "abc.c"); Var ("DEBUG", "false")]
        Steps = [StepInfo ("compile", 217<ms>)]
      }

    let inline (<--) (agent: ^a) (msg: 'b) =
      (^a: (member Post: 'b -> unit) (agent, msg));
      agent

    let inline (<-|) (agent: ^a) (msg: 'b) =
      (^a: (member Post: 'b -> unit) (agent, msg))

    try File.Delete("." </> ".xake") with _ -> ()

    use testee = Storage.openDb "." logger
    testee
      <-- Store (createResult "abc.exe")
      <-- Store (createResult "def.exe")
      <-- Store (createResult "fgh.exe")
      <-| Close
    Threading.Thread.Sleep(1000)

    use testee = Storage.openDb "." logger
    let abc = testee.PostAndReply(fun ch -> GetResult ((FileTarget <| Artifact "abc.exe"),ch))

    Assert.IsTrue(Option.isSome abc)

    let def = testee.PostAndReply(fun ch -> GetResult ((FileTarget <| Artifact "def.exe"),ch))
    Assert.IsTrue(Option.isSome def)

    let fgh = testee.PostAndReply(fun ch -> GetResult ((FileTarget <| Artifact "fgh.exe"),ch))
    Assert.IsTrue(Option.isSome fgh)

    printfn "%A" abc
    testee <-| Close
    Threading.Thread.Sleep(100)

    // TODO Compacting test
