// xake build file
#r @"../bin/Debug/Xake.Core.dll"

open Xake

let mk (x:string) t = x => action {
        do! Async.Sleep(t)
        }

do xake {XakeOptions with FileLog = "build.log"; Threads = 1 } {

  rules [
      "main" <== ["t1"; "t2"]

      mk "t1" 5000
      mk "t2" 4000
      mk "t3" 7000
      mk "t4" 3000
      mk "t5" 5000
  ]

}
