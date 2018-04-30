// xake build file
// #r @"../../bin/Xake.Core.dll"
#r "../../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks.Dotnet

do xake {ExecOptions.Default with Vars = ["NETFX", "4.0"]; FileLogLevel = Verbosity.Diag} {

  rules [

    "main" <== ["hw2.exe"; "hw4.exe"]

    "hw2.exe" ..> recipe {
        do! alwaysRerun()
        do! (csc {
            targetfwk "2.0"
            src (!! "a.cs")
            grefs ["System.dll"]
            define ["TRACE"]
          })
        }
    "hw4.exe" ..> action {
        do! alwaysRerun()
        do! (csc {
            src (!! "a.cs")
            define ["TRACE"]
          })
        }]
}
