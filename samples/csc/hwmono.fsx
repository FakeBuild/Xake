// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with Want = ["hwmono.exe"]; Vars = ["NETFX", "mono-35"]} {

  rules [
    "hwmono.exe" *> fun exe -> action {
        do! (csc {
            out exe
            src (!! "a.cs")
          })
        }]
}
