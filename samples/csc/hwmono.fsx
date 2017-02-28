// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {ExecOptions.Default with Vars = ["NETFX", "mono-35"]} {
  rule ("main" <== ["hwmono.exe"])
  rules [
    //"main" <== ["hwmono.exe"]
    "hwmono.exe" *> fun exe -> csc {
        out exe
        src (!! "a.cs")
      }
  ]
}
