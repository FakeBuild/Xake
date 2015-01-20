// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with Vars = ["NETFX", "mono-35"]} {
  rule ("main" <== ["hwmono.exe"])
  rules [
    //"main" <== ["hwmono.exe"]
    "hwmono.exe" *> fun exe -> action {
        do! (csc {
            out exe
            src (!! "a.cs")
          })
        }]
}
