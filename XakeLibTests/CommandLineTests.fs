namespace XakeLibTests

open System.IO
open NUnit.Framework

open Xake

[<TestFixture (Description = "Command line tests")>]
type CommandLineTests() =

    let currentDir = Path.Combine (__SOURCE_DIRECTORY__, "../bin")

    [<Test (Description = "Verifies reading common switches")>]
    member test.AcceptKnownSwitches() =

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

    [<Test (Description = "Verifies reading target list")>]
    member test.ReadsTargets() =

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


    [<Test (Description = "Verifies reading incorrect"); Ignore>]
    member test.WarnsOnIncorrectSwitch() =

        do xakeArgs ["/xxx"] XakeOptions {
            want ["ss"]
        }

        //raise <| new System.NotImplementedException()

    [<Test (Description = "Verifies that command line is ignored when Ignore option is set")>]
    member test.IgnoresCommandLine() =

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
