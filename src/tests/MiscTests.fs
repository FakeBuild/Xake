namespace Tests

open NUnit.Framework

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

[<TestFixture>]
type ``Various tests``() =
    inherit XakeTestBase("misc")

    let taskReturn n = recipe {
        return n
    }

    [<Test; Ignore("Fails on netcore")>]
    member x.``runs csc task (full test)``() =

        let needExecuteCount = ref 0
        
        do xake {x.TestOptions with FileLog="skipbuild.log"; ConLogLevel = Verbosity.Diag} {  // one thread to avoid simultaneous access to 'wasExecuted'
            want (["hello"])

            rules [
                "hello" ..> recipe {
                    do! trace Error "Running inside 'hello' rule"
                    do! need ["hello.cs"]

                    do! trace Error "Rebuilding..."
                    do! Csc {
                    CscSettings with
                        Src = !!"hello.cs"
                    }
                }
                "hello.cs" ..> action {
                    do! writeText """class Program
                    {
    	                public static void Main()
    	                {
    		                System.Console.WriteLine("Hello world!");
    	                }
                    }"""
                    let! src = getTargetFullName()
                    do! trace Error "Done building 'hello.cs' rule in %A" src
                    needExecuteCount := !needExecuteCount + 1
                }
            ]
        }

        Assert.AreEqual(1, !needExecuteCount)

    [<Test>]
    member x.``resource set instantiation``() =

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

    #if false // !NETCOREAPP2_0
    // this test requires different assembly refs for netcore, and also different way to start
    // I suppose it should be another test for netcore/FAKE 5 env

    // TODO fix the path to Xake assembly

    [<Test>]
    member x.``script exits with errorlevel on script failure``() =

        let fsiApp = if Xake.Env.isRunningOnMono then "fsharpi" else "fsi"
        let errorCode = ref 0
        System.IO.Directory.CreateDirectory("1") |> ignore
        
        do xake {x.TestOptions with FileLog="exits-with-errorlevel.log"; FileLogLevel = Verbosity.Diag; Targets = ["one"] } {
            rules [
                "one" => recipe {
                    do! need ["1/script.fsx"]
                    let! ec = shell {cmd fsiApp; args ["1/script.fsx"]}
                    errorCode := ec
                }
                "1/script.fsx" ..> writeText """
                    #r "../../../bin/Debug/net46/Xake.dll"
                    open Xake

                    do xake {ExecOptions.Default with DbFileName=".1err"; Threads = 4 } {

                    phony "main" (action {
                        do! trace Message "Hello world!"
                        failwith "error-text"
                        })

                    }"""
            ]
        }

        Assert.AreEqual(2, !errorCode)
    #endif

    [<Test>]
    member x.``failif is a short circuit for task result``() =

        let excCount = ref 0
        do xake {x.TestOptions with FileLog="failf.log"} {
            rules [
                "main" => (recipe {
                    do! taskReturn 3 |> FailWhen ((=) 3) "err" |> Recipe.Ignore
                } |> WhenError (fun _ -> excCount := 1))
            ]
        }

        Assert.AreEqual(1, !excCount)

    [<Test>]
    member x.``WhenError handler intercepts the error``() =

        let ex = ref 0

        // pipe result, and provide fallback value in case of error
        do xake {x.TestOptions with FileLog="failf.log"} {
            rules [
                "main" => recipe {
                    do! taskReturn 3
                        |> FailWhen ((=) 3) "fail"
                        |> WhenError (fun _ -> ex := 1; 0)
                        |> Recipe.Ignore
                }
            ]
        }
        // intercept error for resultless action
        do xake {x.TestOptions with FileLog="failf2.log"} {
            rules [
                "main" => recipe {
                    do! taskReturn 3
                        |> FailWhen ((=) 3) "fail"
                        |> Recipe.Ignore
                        |> WhenError (fun _ -> ex := !ex + 1)
                }
            ]
        }

        Assert.AreEqual(2, !ex)
