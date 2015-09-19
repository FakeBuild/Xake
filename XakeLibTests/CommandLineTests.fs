namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake

[<TestFixture>]
type ``Command line interface``() =

    let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "../bin")
    let XakeOptions = ExecOptions.Default

    [<Test>]
    member test.``accepts various switches``() =

        let scriptOptions = ref XakeOptions
        let args =
            ["/t"; "33"; "/R"; currentDir; "/LL"; "Loud";
            "/FL"; "aaaaa"; "/D"; "AA=BBB"; "/D"; "AA1=CCC"; "/FLL"; "Silent"]
    
        do xakeArgs args XakeOptions {
            wantOverride (["test"])

            rules [
              "test" => action {
                  let! opts = getCtxOptions()
                  scriptOptions := opts
              }
            ]
        }

        let finalOptions = !scriptOptions
        Assert.AreEqual(33, finalOptions.Threads)
        Assert.AreEqual(currentDir, finalOptions.ProjectRoot)
        Assert.AreEqual("aaaaa", finalOptions.FileLog)
        Assert.AreEqual(Verbosity.Silent, finalOptions.FileLogLevel)
        Assert.AreEqual(Verbosity.Loud, finalOptions.ConLogLevel)
        Assert.AreEqual([("AA", "BBB"); ("AA1", "CCC")], finalOptions.Vars)

    [<Test>]
    member test.``reads target lists``() =

        let scriptOptions = ref XakeOptions
        let executed2 = ref false
        let args = ["/t"; "31"; "target1"; "target2"]
    
        do xakeArgs args XakeOptions {

            rules [
              "target1" => action {
                  let! opts = getCtxOptions()
                  scriptOptions := opts
              }
              "target2" => action {
                  executed2 := true
              }
            ]
        }

        let finalOptions = !scriptOptions
        Assert.AreEqual(31, finalOptions.Threads)
        Assert.AreEqual(["target1"; "target2"], finalOptions.Targets)
        Assert.IsTrue !executed2


    [<Test; Ignore>]
    member test.``warns on incorrect switch``() =

        do xakeArgs ["/xxx"] XakeOptions {
            want ["ss"]
        }

        //raise <| new System.NotImplementedException()

    [<Test>]
    member test.``supports ignoring command line``() =

        let scriptOptions = ref XakeOptions
        let args =
            ["/t"; "33"; "/R"; currentDir; "/LL"; "Loud";
            "/FL"; "aaaaa"; "target"]
    
        do xakeArgs args {XakeOptions with IgnoreCommandLine = true; Threads = 2; FileLog = "ss"; Targets = ["main"]} {
            rules [
              "main" => action {
                  let! opts = getCtxOptions()
                  scriptOptions := opts
              }
            ]
        }

        let finalOptions = !scriptOptions
        Assert.AreEqual(2, finalOptions.Threads)
        Assert.AreEqual("ss", finalOptions.FileLog)
        Assert.AreEqual(["main"], finalOptions.Targets)
