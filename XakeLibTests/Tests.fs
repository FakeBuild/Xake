namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake

[<TestFixture (Description = "Verious tests")>]
type MiscTests() =

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
    
        do xake {XakeOptions with Threads = 1} {  // one thread to avoid simultaneous access to 'wasExecuted'
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

// this is option2, not implemented
//                  ifneed (action {
////                  let! rebuild = needRebuild
////                  if rebuild = false then
//                    do! writeLog Error "Rebuilding..."
//                    do! Csc {
//                      CscSettings with
//                        Out = file
//                        Src = !!"hello.cs"
//                    }
//                  })

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
              }
            ]
        }

        //Assert.AreEqual(1, !needExecuteCount)
