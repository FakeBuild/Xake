// xake build file
#r @"../bin/Debug/Xake.Core.dll"

open Xake

do xake {XakeOptions with FileLog = "build.log"; Threads = 1 } {

  rules [
      "main" <== ["t2"]

      "t1" => action {
        do! Async.Sleep(5000)
        }
      "t2" => action {
        do! need ["t1"]
        do! Async.Sleep(4000)
        }
  ]

}
