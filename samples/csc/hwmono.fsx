// xake build file
//#r @"../../bin/Xake.Core.dll"
#r "../../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks.Dotnet

do xake {ExecOptions.Default with Vars = ["NETFX", "mono-35"]} {
  rule ("main" <== ["hwmono.exe"])
  rules [
    //"main" <== ["hwmono.exe"]
    "hwmono.exe" ..> csc {src !! "a.cs"}
  ]
}
