// xake build file
#r @"../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with FileLog = "build.log"; Threads = 4 } {

  want ["main"]

  phony "main" (action {
    do! writeLog Message "Hello world!"
    })

}
