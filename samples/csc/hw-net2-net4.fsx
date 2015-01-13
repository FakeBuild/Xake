// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with Want = ["hw2.exe"; "hw4.exe"]; Vars = ["NETFX", "4.0"]; FileLogLevel = Verbosity.Diag} {

  rules [
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
