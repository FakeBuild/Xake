// xake build file
#r @"../bin/Debug/Xake.Core.dll"

open Xake

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  phony "main" (action {
    do! trace Message "The exception thrown below will be silently ignored"
    failwith "some error"
    } |> WhenError ignore)

}
