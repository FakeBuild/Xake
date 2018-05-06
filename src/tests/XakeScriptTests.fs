module ``Xake script``

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks
open Storage

let private rememberDir = Directory.GetCurrentDirectory()
let private outFolder = rememberDir </> "~testout~" </> "rm"

[<OneTimeSetUp>]
let setup () =
    Directory.CreateDirectory outFolder |> ignore
    Directory.SetCurrentDirectory outFolder

[<OneTimeTearDown>]
let teardown () =
    Directory.SetCurrentDirectory rememberDir

[<SetUp>]
let setupTest () =
    "." </> ".xake" |> File.Delete

// one thread to avoid simultaneous access to 'wasExecuted'
let XakeOptions = {ExecOptions.Default with FileLog = ""; Threads = 1; ProjectRoot = outFolder}

[<Test>]
let ``executes dependent rules (once)``() =

    let wasExecuted = ref []
    let needExecuteCount = ref 0
    
    do xake XakeOptions {
        want (["test"; "test1"])

        rules [
            "test" => action {
                do! trace Error "Running inside 'test' rule"
                do! need ["aaa"]
                wasExecuted := ("test" :: !wasExecuted)
            }
            "test1" => action {
                do! trace Error "Running inside 'test1' rule"
                do! need ["aaa"]
                wasExecuted := ("test1" :: !wasExecuted)
            }
            "aaa" => action {
                needExecuteCount := !needExecuteCount + 1
            }
        ]
    }

    Assert.IsTrue(!wasExecuted |> List.exists ((=) "test"))
    Assert.IsTrue(!wasExecuted |> List.exists ((=) "test1"))

    Assert.AreEqual(1, !needExecuteCount)

[<Test>]
let ``executes need only once``() =

    let needExecuteCount = ref 0
    File.WriteAllText("hlo.cs", "empty file")
    
    let build () = xake XakeOptions {
        want (["hlo"])

        rule ("hlo" ..> action {
            do! trace Error "Running inside 'hlo' rule"
            do! need ["hlo.cs"]
            needExecuteCount := !needExecuteCount + 1
            do! Async.Sleep 2000
            do! writeText ""
        })
    }

    do build()
    Assert.AreEqual(1, !needExecuteCount)

    // now build it again and ensure the count is not changed
    do build()
    Assert.AreEqual(1, !needExecuteCount)

    // now "touch" the file
    File.AppendAllText("hlo.cs", "ee")
    do build()
    Assert.AreEqual(2, !needExecuteCount)

[<Test>]
let ``allows defining conditional rule``() =

    let needExecuteCount = ref 0
    
    let build () = xake XakeOptions {
        want (["hlo"])

        rules [
            // TODO want ((=) "hlo") *?> ...
            (fun n -> n.EndsWith("hlo")) ..?> action {
                do! trace Error "Running inside 'hlo' rule"
                needExecuteCount := !needExecuteCount + 1
                do! Async.Sleep 500
            }
        ]
    }

    do build()
    Assert.AreEqual(1, !needExecuteCount)

[<Test>]
let ``rebuilds when fileset is changed``() =

    let needExecuteCount = ref 0

    Directory.EnumerateFiles(".", "hello*.cs") |> Seq.iter File.Delete

    File.WriteAllText("hello.cs", "empty file")
    
    let build () = xake XakeOptions {
        want (["hello"])

        rules [
            "hello" ..> recipe {
                do! trace Error "Running inside 'hello' rule"
                let! files = (!!"hello*.cs") |> getFiles 
                do! needFiles files
                needExecuteCount := !needExecuteCount + 1
                do! writeText ""
            }
        ]
    }

    do build()
    do build()
    Assert.AreEqual(1, !needExecuteCount)

    // now add another file and make sure target ir rebuilt
    File.WriteAllText("hello1.cs", "empty file")
    do build()
    Assert.AreEqual(2, !needExecuteCount)

[<Test>]
let ``rebuilds when env variable is changed``() =

    let needExecuteCount = ref 0
    File.WriteAllText("hlo.cs", "empty file")

    System.Environment.SetEnvironmentVariable("TTT", "1")
    
    let build () = xake XakeOptions {
        want (["hlo"])

        rules [
            "hlo" ..> recipe {
                do! need ["hlo.cs"]
                let! var = getEnv("TTT")
                do! trace Command "Running inside 'hlo' rule with var:%A" var
                needExecuteCount := !needExecuteCount + 1
                do! Async.Sleep 2000
                do! writeText ""
            }
        ]
    }

    do build()
    Assert.AreEqual(1, !needExecuteCount)

    // now build it again and ensure the count is not changed
    do build()
    Assert.AreEqual(1, !needExecuteCount)

    // now "touch" the file
    System.Environment.SetEnvironmentVariable("TTT", "2")

    do build()
    Assert.AreEqual(2, !needExecuteCount)

    System.Environment.SetEnvironmentVariable("TTT", "")

[<Test>]
let ``defaults target to `main` ``() =

    let count = ref 0
    
    do xake XakeOptions {
        rules [
            "main" => action {
                count := !count + 1
            }
        ]
    }

    Assert.AreEqual(1, !count)

[<Test>]
let ``allows to define target in parameters``() =

    let mainCount = ref 0
    let xxxCount = ref 0
    
    do xake {XakeOptions with Targets = ["xxx"]} {
        rules [
            "main" => action {
                mainCount := !mainCount + 1
            }
            "xxx" => action {
                xxxCount := !xxxCount + 1
            }
        ]
    }

    Assert.AreEqual(0, !mainCount)
    Assert.AreEqual(1, !xxxCount)

[<Test; Platform("Win"); Explicit("Won't run on linux")>]
let ``target could be a relative``() =

    let needExecuteCount = ref 0

    let subdir = Directory.CreateDirectory "subd1"
    let preserve_dir = System.Environment.CurrentDirectory
    System.Environment.CurrentDirectory <- subdir.FullName

    try
        Directory.EnumerateFiles(".", "hello*.cs") |> Seq.iter File.Delete

        File.WriteAllText("hello.cs", "empty file")
    
        do xake XakeOptions {
            rules [
                "main" <== ["../subd1/a.ss"]
                "../subd1/a.ss" ..> action {
                    let! outName = getTargetFullName()
                    do! trace Error "Running inside 'a.ss' rule"
                    needExecuteCount := !needExecuteCount + 1
                    File.WriteAllText(outName, "ss")
                }
            ]
        }

        Assert.AreEqual(1, !needExecuteCount)

    finally
        System.Environment.CurrentDirectory <- preserve_dir

[<Test()>]
let ``groups in rule pattern``() =

    let matchedAny = ref false

    do xake {XakeOptions with Targets = ["out/abc.ss"]} {
        rule ("(dir:*)/(file:*).(ext:ss)" ..> recipe {
            
            let! groups = getRuleMatches()
            Assert.AreEqual("out", groups.["dir"])
            Assert.AreEqual("abc", groups.["file"])
            Assert.AreEqual("ss",  groups.["ext"])
            matchedAny := true
        })
    }

    Assert.IsTrue(!matchedAny)

[<TestCase("x86-a.ss", "(plat:*)-a.ss", "plat:x86", TestName="Simple case")>]
[<TestCase("subd1/x86-a.ss", "(dir:*)/(plat:*)-a.ss", "dir:subd1;plat:x86", TestName="groups in various parts")>]
[<TestCase("subd1/x86-a.ss", "(root:*/*-a).ss", "root:subd1/x86-a", TestName="group across parts")>]
[<TestCase("x86-a.ss", "(name:(plat:*)-a.ss)", "plat:x86;name:x86-a.ss", TestName="Nested groups")>]
[<TestCase("(abc.ss", @"(name:[(]*).ss", "name:(abc", TestName="Escaped brackets")>]
let ``matching groups in rule name``(tgt,mask,expect:string) =

    let mutable map = Map.empty
    let mutable matchedAny = false

    do xake {XakeOptions with Targets = [tgt]} {
        rule (mask ..> recipe {
            let! groups = getRuleMatches()
            map <- groups; matchedAny <- true
        })
    }

    Assert.IsTrue matchedAny

    if System.String.IsNullOrEmpty expect then
        map |> Map.isEmpty |> Assert.IsTrue
    else
        let expected = expect.Split(';') |> Array.map (fun s -> let pp = s.Split(':') in pp.[0],pp.[1])
        let leaveNamedGroups (k:string) v = not (System.Char.IsDigit k.[0])
        Assert.That(map |> Map.filter leaveNamedGroups |> Map.toArray, Is.EquivalentTo(expected))

[<TestCase("dd/a.dll", "dd/*.dll;dd/*.xml", "dd/a.dll;dd/a.xml", TestName="Simple case")>]
[<TestCase("dd/ff/a.dll", "**/*.dll;**/*.xml", "dd/ff/a.dll;dd/ff/a.xml", TestName="Recurse mask")>]
[<TestCase("dd/a.dll", "(pat:**/*).dll;(pat:**/*)/main.xml", "dd/a.dll;dd/a/main.xml", TestName="Masks")>]
let ``multitarget rule generates names``(tgt,(masksStr: string),expect:string) =

    let masks = masksStr.Split ';'
    let map = ref Map.empty
    let mutable matchedAny = false
    let mutable fileNames = []
    let mutable root = ""    

    do xake {XakeOptions with Targets = [tgt]} {
        rule (masks *..> recipe {
            let! groups = getRuleMatches()
            let! files = getTargetFiles()
            let! options = getCtxOptions()
            root <- options.ProjectRoot
            fileNames <- (files |> List.map File.getFullName)
            map := groups; matchedAny <- true
        })
    }

    Assert.IsTrue matchedAny
    Assert.That(fileNames, Is.All.StartsWith root)
    let relFileNames = fileNames |> List.map (fun s -> s.Substring(root.Length + 1).Replace("\\", "/"))

    Assert.That(relFileNames, expect.Split(';') |> Is.EquivalentTo)

[<TestCase("x86-a.ss", "(plat:*)-a.ss", "plat", ExpectedResult = "x86", TestName="Simple case")>]
[<TestCase("subd1/x86-a.ss", "*/(plat:*)-a.ss", "plat", ExpectedResult = "x86", TestName="groups in various parts")>]
[<TestCase("(abc.ss", @"[(]*.ss", "plat", ExpectedResult = "", TestName="Escaped brackets")>]
[<TestCase("filename.ss", "*.ss", "1", ExpectedResult = "filename", TestName="Match wildcards")>]
let ``getRuleMatch() matches part``(tgt,mask,tag) =

    let resultValue = ref ""

    do xake {XakeOptions with Targets = [tgt]} {
        rule (mask ..> recipe {
            let! value = getRuleMatch tag
            resultValue := value
        })
    }

    !resultValue

type Runtime = {Ver: string; Folder: string}

// TODO make correct test

[<Test; Platform("Win"); Explicit("Won't run on linux")>]
let ``target could be a relative2``() =

    let needExecuteCount = ref 0

    let subdir = Directory.CreateDirectory "subd1"
    let preserveDir = System.Environment.CurrentDirectory
    System.Environment.CurrentDirectory <- subdir.FullName

    try
        let runtimes =
          [
            {Ver = "v9"; Folder = @"c:\!\aaa\Runtimes\9.1.2134.0"}
            {Ver = "v10"; Folder = @"c:\!\aaa\Runtimes\10.99.5262.0"}]

        let pcExeName = "PerformanceComparer.exe"

        let copyToOutputAndRename target src = target ..> copyFrom src

        let makeRule runtime =
            let folder = System.Environment.CurrentDirectory </> runtime.Folder
            [
            (folder </> pcExeName) ..> recipe {
                let! exe = getTargetFullName()
                do! need [exe + ".config"]
                needExecuteCount := !needExecuteCount + 1
                }
            (runtime.Folder </> pcExeName + ".config") ..> recipe {()}
            ]

        do xake {ExecOptions.Default with FileLogLevel=Verbosity.Diag; FileLog = "build123.log"} {

          rules (runtimes |> List.collect makeRule)
          rule ("main" <== [for r in runtimes do yield r.Folder </> pcExeName])

        }

        Assert.AreEqual(2, !needExecuteCount)

    finally
        System.Environment.CurrentDirectory <- preserveDir

#endif

[<Test>]
let ``executes several dependent rules``() =

    let count = ref 0
    
    do xake XakeOptions {
        rules [
            "main" <== ["rule1"; "rule2"]
            "rule1" ..> recipe {
                count := !count + 1
            }
            "rule2" ..> recipe {
                count := !count + 10
            }
        ]
    }

    Assert.AreEqual(11, !count)

[<Test>]
let ``executes in parallel``() =

    let steps = System.Collections.Generic.List<int>()
    
    do xake { XakeOptions with Threads = 4 } {
        rules [
            "main" <== ["rule1"; "rule2"; "rule3"]
            "rule1" => action {
                do! Async.Sleep(40)
                steps.Add 1
            }
            "rule2" => action {
                do! Async.Sleep(20)
                steps.Add 2
            }
            "rule3" => action {
                do! Async.Sleep(10)
                steps.Add 3
            }
        ]
    }

    Assert.That(steps, Is.EqualTo([3; 2; 1] |> List.toArray))

[<Test>]
let ``op <<< executes one by one``() =

    let steps = System.Collections.Generic.List<int>()
    
    do xake { XakeOptions with Threads = 4 } {
        rules [
            "main" <<< ["rule1"; "rule2"; "rule3"]
            "rule1" => action {
                do! Async.Sleep(40)
                steps.Add 1
            }
            "rule2" => action {
                do! Async.Sleep(20)
                steps.Add 2
            }
            "rule3" => action {
                do! Async.Sleep(10)
                steps.Add 3
            }
        ]
    }

    Assert.AreEqual(steps, [1; 2; 3] |> List.toArray)

[<Test>]
let ``writes dependencies to a build database``() =

    let cdate = System.DateTime(2014, 1, 2, 3, 40, 50)

    // execute script, check what's in build log
    File.WriteAllText ("bbb.c", "// empty file")
    FileInfo("bbb.c").LastWriteTime <- cdate
    
    do xake XakeOptions {
        want (["test"; "test1"])

        rules [
            "test" => action {
                do! need ["aaa"]

                // check nested actions are also collected
                do! action {
                do! action {
                    do! need ["deeplyNested"]
                    do! need ["bbb.c"]
                }
                }
            }
            "test1" => action {
                do! need ["aaa"]
            }
            "aaa" => action {
            return ()
            }
            "deeplyNested" => action {
            return ()
            }
        ]
    }

    use testee = Storage.openDb "./.xake" (ConsoleLogger Verbosity.Diag)
    try
        match testee.PostAndReply <| fun ch -> DatabaseApi.GetResult ((PhonyAction "test"), ch) with
        | Some {
                BuildResult.Targets = [PhonyAction "test"]
                Depends = [
                            ArtifactDep (PhonyAction "aaa"); ArtifactDep (PhonyAction "deeplyNested");
                            FileDep (fileDep, depDate)
                            ]
            }
            when System.IO.Path.GetFileName(fileDep.Name) = "bbb.c" && depDate = cdate
            -> true
        | a -> false
        |> Assert.True

        match testee.PostAndReply <| fun ch -> DatabaseApi.GetResult ((PhonyAction "test1"), ch) with
        | Some {
                Targets = [PhonyAction "test1"]
                Depends = [ArtifactDep (PhonyAction "aaa")]
                //BuildResult.Steps = []
            } -> true
        | _ -> false
        |> Assert.True

    finally
        testee.PostAndReply DatabaseApi.CloseWait
    ()

[<Test>]
let ``writes a build stats to a database``() =
    let xdb = "." </> ".xake"
    do File.Delete xdb

    do xake {XakeOptions with Threads = 1} {
        rules [
            "main" => action {
                do! need ["aaa"; "bbb"]
            }

            "aaa" => action {
                do! newstep "a"
                do! Async.Sleep 100
                do! newstep "b"
                do! Async.Sleep 70
            }
            "bbb" ..> action {
                do! Async.Sleep 200
            }
        ]
    }

    let (<-*) (a:Agent<DatabaseApi>) t = a.PostAndReply(fun ch -> GetResult (t,ch))
    let logger = ConsoleLogger Verbosity.Diag

    use db = Storage.openDb "./.xake" logger
    try
        let (Some {Steps = step1::_}) = db <-* (PhonyAction "main")
        Assert.That(step1.WaitTime, Is.GreaterThanOrEqualTo(370))

        let raaa = db <-* (PhonyAction "aaa")
        printfn "%A" raaa
    finally
        db.PostAndReply CloseWait

[<Test>]
let ``dryrun for not executing``() =

    let count = ref 0
    
    do xake XakeOptions {
        dryrun
        filelog "errors.log" Verbosity.Chatty
        rules [
            "main" <== ["rule1"; "rule2"]
            "rule1" => action {
                count := !count + 1
            }
            "rule2" => action {
                count := !count + 10
            }
        ]
    }

    Assert.AreEqual(0, !count)
