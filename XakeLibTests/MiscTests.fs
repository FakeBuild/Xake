module ``Various tests``

open System.IO
open NUnit.Framework

open Xake
open Xake.SystemTasks

let xakeOptions = ExecOptions.Default

[<SetUp>]
let Setup() =
    try File.Delete("." </> ".xake") with _ -> ()

[<Test>]
let ``runs csc task (full test)``() =

    let needExecuteCount = ref 0
    
    do xake {xakeOptions with Threads = 1; FileLog="skipbuild.log"} {  // one thread to avoid simultaneous access to 'wasExecuted'
        want (["hello"])

        rules [
            "hello" *> fun file -> action {
                do! trace Error "Running inside 'hello' rule"
                do! need ["hello.cs"]

                do! trace Error "Rebuilding..."
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
                do! trace Error "Done building 'hello.cs' rule in %A" src
                needExecuteCount := !needExecuteCount + 1
            }
        ]
    }

    Assert.AreEqual(1, !needExecuteCount)

[<Test>]
let ``resource set instantiation``() =

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

[<Test>]
let ``script exits with errorlevel on script failure``() =

    let fsiApp = if Xake.Env.isRunningOnMono then "fsharpi" else "fsi"
    let errorCode = ref 0
    System.IO.Directory.CreateDirectory("1") |> ignore
    
    do xake {xakeOptions with Threads = 1; FileLog="exits-with-errorlevel.log"; FileLogLevel = Verbosity.Diag; Targets = ["one"] } {
        rules [
            "one" => action {
                do! need ["1/script.fsx"]
                let! ec = system id fsiApp ["1/script.fsx"]
                errorCode := ec
            }
            "1/script.fsx" *> fun src -> action {
                do File.WriteAllText (src.FullName, """
#r "../Xake.Core.dll"
open Xake

do xake {ExecOptions.Default with DbFileName=".1err"; Threads = 4 } {

  phony "main" (action {
    do! trace Message "Hello world!"
    failwith "error-text"
    })

}
""")
            }
        ]
    }

    Assert.AreEqual(2, !errorCode)

let taskReturn n = action {
    return n
}

[<Test>]
let ``failif is a short circuit for task result``() =

    let excCount = ref 0
    do xake {xakeOptions with Threads = 1; FileLog="failf.log"} {
        rules [
            "main" => (action {
                do! taskReturn 3 |> FailWhen ((=) 3) "err" |> Action.Ignore
            } |> WhenError (fun _ -> excCount := 1))
        ]
    }

    Assert.AreEqual(1, !excCount)

[<Test>]
let ``WhenError handler intercepts the error``() =

    let ex = ref 0

    // pipe result, and provide fallback value in case of error
    do xake {xakeOptions with Threads = 1; FileLog="failf.log"} {
        rules [
            "main" => action {
                do! taskReturn 3
                    |> FailWhen ((=) 3) "fail"
                    |> WhenError (fun _ -> ex := 1; 0)
                    |> Action.Ignore
            }
        ]
    }
    // intercept error for resultless action
    do xake {xakeOptions with Threads = 1; FileLog="failf.log"} {
        rules [
            "main" => action {
                do! taskReturn 3
                    |> FailWhen ((=) 3) "fail"
                    |> Action.Ignore
                    |> WhenError (fun _ -> ex := !ex + 1)
            }
        ]
    }

    Assert.AreEqual(2, !ex)
