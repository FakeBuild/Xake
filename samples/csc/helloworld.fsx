// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with FileLog = "build.log"; Threads = 4 } {

  want (["hw.exe"])

  rule("hw.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a.cs"
      }
    })
}
