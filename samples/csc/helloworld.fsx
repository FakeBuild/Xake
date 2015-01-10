// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

let mainRule = "hw.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a.cs"
      }
    }

do xake {XakeOptions with Threads = 4} {

  want (["build"])

  phony "build" (action {
      do! need ["hw.exe"]
      })

  rule mainRule
}