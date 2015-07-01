namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake
open Storage
open BuildLog

[<TestFixture (Description = "Various tests")>]
type MiscTests() =

    [<SetUp>]
    member test.Setup() =
      try File.Delete("." </> ".xake") with _ -> ()

    [<Test (Description = "Verifies need is executed only once")>]
    member test.NeedExecutesOnes() =

        let wasExecuted = ref []
        let needExecuteCount = ref 0
    
        do xake {XakeOptions with Threads = 1} {  // one thread to avoid simultaneous access to 'wasExecuted'
            want (["test"; "test1"])

            rules [
              "test" => action {
                  do! writeLog Error "Running inside 'test' rule"
                  do! need ["aaa"]
                  wasExecuted := ("test" :: !wasExecuted)
              }
              "test1" => action {
                  do! writeLog Error "Running inside 'test1' rule"
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

    [<Test (Description = "Verifies need is executed only once")>]
    member test.SkipTask() =

        let needExecuteCount = ref 0
    
        do xake {XakeOptions with Threads = 1; FileLog="skipbuild.log"} {  // one thread to avoid simultaneous access to 'wasExecuted'
            want (["hello"])

            rules [
              "hello" *> fun file -> action {
                  do! writeLog Error "Running inside 'hello' rule"
                  do! need ["hello.cs"]

                  do! writeLog Error "Rebuilding..."
                  do! Csc {
                    CscSettings with
                      Out = file
                      Src = !!"hello.cs"
                  }
              }
              "hello.cs" *> fun src -> action {
                  do File.WriteAllText (src.FullName, """class Program
                    {
	                    public static void Main()
	                    {
		                    System.Console.WriteLine("Hello world!");
	                    }
                    }""")
                  do! writeLog Error "Done building 'hello.cs' rule in %A" src
                  needExecuteCount := !needExecuteCount + 1
              }
            ]
        }

        Assert.AreEqual(1, !needExecuteCount)

    [<Test (Description = "Verifies need is executed only once")>]
    member test.BuildLog() =

        let cdate = System.DateTime(2014, 1, 2, 3, 40, 50);

        // execute script, check what's in build log
        File.WriteAllText ("bbb.c", "// empty file")
        FileInfo("bbb.c").LastWriteTime <- cdate
    
        do xake {XakeOptions with Threads = 1} {  // one thread to avoid simultaneous access to 'wasExecuted'
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
              | Some
                {
                    BuildResult.Result = PhonyAction "test"
                    BuildResult.Depends
                      = [
                        ArtifactDep (PhonyAction "aaa"); ArtifactDep (PhonyAction "deeplyNested");
                        Dependency.File (fileDep, depDate)
                      ]
                    //BuildResult.Steps = []
                }
                when System.IO.Path.GetFileName(fileDep.Name) = "bbb.c" && depDate = cdate
                 -> true
              | a -> false

          Assert.IsTrue <|
            match testee.PostAndReply <| fun ch -> DatabaseApi.GetResult ((PhonyAction "test1"), ch) with
              | Some
                {
                    BuildResult.Result = PhonyAction "test1"
                    BuildResult.Depends = [ArtifactDep (PhonyAction "aaa")]
                    //BuildResult.Steps = []
                } -> true
              | _ -> false

        finally
          testee.PostAndReply DatabaseApi.CloseWait

    [<Test (Description = "Verifies need is executed only once")>]
    member test.SkipOnRebuild() =

        let needExecuteCount = ref 0
        File.WriteAllText("hlo.cs", "empty file")
    
        let build () = xake {XakeOptions with Threads = 1; FileLog=""} {
            want (["hlo"])

            rules [
              "hlo" *> fun file -> action {
                  do! writeLog Error "Running inside 'hlo' rule"
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

    [<Test (Description = "Verifies conditional rule execution")>]
    member test.ConditionalRule() =

        let needExecuteCount = ref 0
    
        let build () = xake {XakeOptions with Threads = 1; FileLog=""} {
            want (["hlo"])

            rules [
              // TODO want ((=) "hlo") *?> ...
              (fun n -> n.EndsWith("hlo")) *?> fun file -> action {
                  do! writeLog Error "Running inside 'hlo' rule"
                  needExecuteCount := !needExecuteCount + 1
                  do! Async.Sleep 500
              }
            ]
        }

        do build()
        Assert.AreEqual(1, !needExecuteCount)

    [<Test (Description = "Verifies rebuild is done when fileset is changed")>]
    member test.RebuildOnNewFile() =

        let needExecuteCount = ref 0

        Directory.EnumerateFiles(".", "hello*.cs") |> Seq.iter File.Delete

        File.WriteAllText("hello.cs", "empty file")
    
        let build () = xake {XakeOptions with Threads = 1; FileLog=""} {
            want (["hello"])

            rules [
              "hello" *> fun file -> action {
                  do! writeLog Error "Running inside 'hello' rule"
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

    [<Test (Description = "Verifies rebuild on var change")>]
    member test.BuildOnEnvChange() =

        let needExecuteCount = ref 0
        File.WriteAllText("hlo.cs", "empty file")

        System.Environment.SetEnvironmentVariable("TTT", "1")
    
        let build () = xake {XakeOptions with Threads = 1; FileLog=""} {
            want (["hlo"])

            rules [
              "hlo" *> fun file -> action {
                  do! need ["hlo.cs"]
                  let! var = getEnv("TTT")
                  do! writeLog Command "Running inside 'hlo' rule with var:%A" var
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

    [<Test (Description = "Verifies resource set instantiation")>]
    member this.NewResourceSet() =

        let resset = resourceset {
            prefix "Sample.Application"
            dynamic true

            files (fileset {
                includes "*.resx"
            })
        }

        let resourceSetCollection = [
            resourceset {
                prefix "Sample.Application"
                dynamic true

                files (fileset {
                    includes "*.resx"
                })
            }
            resourceset {
                prefix "Sample.Application1"
                dynamic true

                files (fileset {
                    includes "*.res"
                })
            }
        ]

        printfn "%A" resset
        ()

    [<Test (Description = "Verifies main is a default target")>]
    member test.MainTargetIsDefault() =

        let count = ref 0
    
        do xake {XakeOptions with Threads = 1; FileLog=""} {  // one thread to avoid simultaneous access to 'wasExecuted'
            rules [
              "main" => action {
                  count := !count + 1
              }
            ]
        }

        Assert.AreEqual(1, !count)

    [<Test (Description = "Verifies main is a default target")>]
    member test.ExplicitlySetTarget() =

        let count = ref 0
    
        do xake {XakeOptions with Want = ["xxx"]; Threads = 1; FileLog=""} {  // one thread to avoid simultaneous access to 'wasExecuted'
            rules [
              "main" => action {
                  count := !count + 1
              }
            ]
        }

        Assert.AreEqual(0, !count)

    [<Test (Description = "Verifies need is executed only once")>]
    member test.DependentTargetsRule() =

        let count = ref 0
    
        do xake {XakeOptions with Threads = 1; FileLog=""} {  // one thread to avoid simultaneous access to 'wasExecuted'
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
    member this.BuildStats() =
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
      