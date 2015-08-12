namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake
open Storage
open BuildLog

[<TestFixture (Description = "Various tests")>]
type MiscTests() =

    let XakeOptions = ExecOptions.Default

    [<SetUp>]
    member test.Setup() =
      try File.Delete("." </> ".xake") with _ -> ()

    [<Test>]
    member test.``runs csc task (full test)``() =

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

