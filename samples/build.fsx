// xake build file
#r @"../bin/Xake.Core.dll"

open Xake

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  phony "main" (action {
    do! writeLog Message "Hello world!"
    })

}
