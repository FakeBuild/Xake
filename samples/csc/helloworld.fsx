// xake build file

#r @"../../bin/Xake.Core.dll"

open Xake
open System

do xake {XakeOptions with FileLog = "build.log"} {

  rule ("main" ==> ["hw.exe"])

  rule("hw.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a.cs"
      }
    })
}
