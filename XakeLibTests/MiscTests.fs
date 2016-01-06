module ``Various tests``

open System.IO
open NUnit.Framework

open Xake

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

