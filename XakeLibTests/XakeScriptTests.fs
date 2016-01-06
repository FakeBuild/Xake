module ``Xake script``

open System.IO
open NUnit.Framework

open Xake
open Storage

// one thread to avoid simultaneous access to 'wasExecuted'
let XakeOptions = {ExecOptions.Default with FileLog = ""; Threads = 1}

[<SetUp>]
let Setup() =
    try File.Delete("." </> ".xake") with _ -> ()

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

        rules [
            "hlo" *> fun file -> action {
                do! trace Error "Running inside 'hlo' rule"
                do! need ["hlo.cs"]
                needExecuteCount := !needExecuteCount + 1
                do! Async.Sleep 2000
                File.WriteAllText(file.FullName, "")
            }
        ]
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
            (fun n -> n.EndsWith("hlo")) *?> fun file -> action {
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
            "hello" *> fun file -> action {
                do! trace Error "Running inside 'hello' rule"
                let! files = (!!"hello*.cs") |> getFiles 
                do! needFiles files
                needExecuteCount := !needExecuteCount + 1
                File.WriteAllText(file.FullName, "")
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
            "hlo" *> fun file -> action {
                do! need ["hlo.cs"]
                let! var = getEnv("TTT")
                do! trace Command "Running inside 'hlo' rule with var:%A" var
                needExecuteCount := !needExecuteCount + 1
                do! Async.Sleep 2000
                File.WriteAllText(file.FullName, "")
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

[<Test>]
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
                "../subd1/a.ss" %> fun out -> action {
                    do! trace Error "Running inside 'a.ss' rule"
                    needExecuteCount := !needExecuteCount + 1
                    File.WriteAllText(out.fullname, "ss")
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
        rule ("(dir:*)/(file:*).(ext:ss)" %> fun out -> action {
            
            Assert.AreEqual("out", out.group "dir")
            Assert.AreEqual("abc", out.group "file")
            Assert.AreEqual("ss", out.group "ext")
            matchedAny := true
        })
    }

    Assert.IsTrue(!matchedAny)


[<TestCase("x86-a.ss", "(plat:*)-a.ss", "plat:x86", TestName="Simple case")>]
[<TestCase("subd1/x86-a.ss", "(dir:*)/(plat:*)-a.ss", "dir:subd1;plat:x86", TestName="groups in various parts")>]
[<TestCase("x86-a.ss", "(name:(plat:*)-a.ss)", "plat:x86;name:x86-a.ss", TestName="Nested groups")>]
[<TestCase("(abc.ss", @"[(]*.ss", "", TestName="Escaped brackets")>]
let ``matching groups in rule name``(tgt,mask,expect:string) =

    let map = ref Map.empty
    let matchedAny = ref false

    do xake {XakeOptions with Targets = [tgt]} {
        rule (mask %> fun out -> action {
            map := out.allGroups; matchedAny := true
        })
    }

    Assert.IsTrue(!matchedAny)

    if System.String.IsNullOrEmpty expect then
        Assert.IsTrue(!map |> Map.isEmpty)
    else
        let expected = expect.Split(';') |> Array.map (fun s -> let pp = s.Split(':') in pp.[0],pp.[1])
        Assert.That(!map |> Map.toArray, Is.EquivalentTo(expected))

type Runtime = {Ver: string; Folder: string}

[<Test>]
let ``target could be a relative2``() =

    let needExecuteCount = ref 0

    let subdir = Directory.CreateDirectory "subd1"
    let preserve_dir = System.Environment.CurrentDirectory
    System.Environment.CurrentDirectory <- subdir.FullName

    try
        let Runtimes = [
            {Ver = "v9"; Folder = @"c:\\!\\aaa\Runtimes\9.1.2134.0"}
            {Ver = "v10"; Folder = @"c:\\!\\aaa\Runtimes\10.99.5262.0"}]

        let pc_exe = "PerformanceComparer.exe"

        let copyToOutputAndRename target src = target *> fun outfile -> action {do! cp src outfile.FullName}

        let makeRule runtime =
            let folder = System.Environment.CurrentDirectory </> runtime.Folder
            [
            (folder </> pc_exe) *> fun exe -> action {
                do! need [exe.FullName + ".config"]
                needExecuteCount := !needExecuteCount + 1
                }
            (runtime.Folder </> pc_exe + ".config") *> fun outfile -> action {()}
            ]

        do xake {ExecOptions.Default with FileLogLevel=Verbosity.Diag; FileLog = "build.log"} {

          rules (Runtimes |> List.collect makeRule)
          rule ("main" ==> [for r in Runtimes do yield r.Folder </> pc_exe])

        }

        Assert.AreEqual(2, !needExecuteCount)

    finally
        System.Environment.CurrentDirectory <- preserve_dir

[<Test>]
let ``executes several dependent rules``() =

    let count = ref 0
    
    do xake XakeOptions {
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

    Assert.AreEqual(11, !count)

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

    use testee = Storage.openDb "." (ConsoleLogger Verbosity.Diag)
    try

        Assert.IsTrue <|
          match testee.PostAndReply <| fun ch -> DatabaseApi.GetResult ((PhonyAction "test"), ch) with
            | Some {
                    BuildResult.Result = PhonyAction "test"
                    BuildResult.Depends
                        = [
                        ArtifactDep (PhonyAction "aaa"); ArtifactDep (PhonyAction "deeplyNested");
                        FileDep (fileDep, depDate)
                        ]
                }
                when System.IO.Path.GetFileName(fileDep.Name) = "bbb.c" && depDate = cdate
                -> true
            | a -> false

        Assert.IsTrue <|
            match testee.PostAndReply <| fun ch -> DatabaseApi.GetResult ((PhonyAction "test1"), ch) with
            | Some {
                    BuildResult.Result = PhonyAction "test1"
                    BuildResult.Depends = [ArtifactDep (PhonyAction "aaa")]
                    //BuildResult.Steps = []
                } -> true
            | _ -> false

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
            "bbb" *> fun file -> action {
                do! Async.Sleep 200
            }
        ]
    }

    let (<-*) (a:Agent<DatabaseApi>) t = a.PostAndReply(fun ch -> GetResult (t,ch))
    let logger = ConsoleLogger Verbosity.Diag

    use db = Storage.openDb "." logger
    try
        let (Some {Steps = step1::_}) = db <-* (PhonyAction "main")
        Assert.That(step1.WaitTime, Is.GreaterThanOrEqualTo(370))

        let raaa = db <-* (PhonyAction "aaa")
        printfn "%A" raaa
    finally
        db.PostAndReply CloseWait
      