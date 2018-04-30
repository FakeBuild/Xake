// xake build file

// #r @"../../bin/Xake.Core.dll"
#r "../../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks.Dotnet

do xake {ExecOptions.Default with FileLog = "build.log"} {

  rule ("main" <== ["hw.exe"])

  rule("hw.exe" ..> Csc {
      CscSettings with
        Src = !! "a.cs"
      })
}
