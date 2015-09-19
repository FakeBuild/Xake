// xake build file
#r @"../../bin/Debug/Xake.Core.dll"

open Xake

do xake {ExecOptions.Default with Vars = ["NETFX", "4.0"]; FileLogLevel = Verbosity.Diag} {

  rules [

    "main" <== ["check_deps"]
    //"main" <== ["hw2.exe"; "hw4.exe"]

    "check_deps" => action {
        let! ctx = getCtx()
        let tgt = FileTarget (Artifact (ctx.Options.ProjectRoot </> "hw2.exe"))
        let reasons = getDirtyState ctx tgt

        printfn "need: %A" reasons
    }

    "hw2.exe" *> fun exe -> action {
        do! alwaysRerun()
        do! (csc {
            out exe
            targetfwk "2.0"
            src (!! "a.cs")
            grefs ["System.dll"]
            define ["TRACE"]
          })
        }
    "hw4.exe" *> fun exe -> action {
        do! alwaysRerun()
        do! (csc {
            out exe
            src (!! "a.cs")
            define ["TRACE"]
          })
        }]
}
