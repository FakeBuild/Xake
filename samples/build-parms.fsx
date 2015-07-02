// xake build file
#r @"../bin/Debug/Xake.Core.dll"

open Xake

do xake {XakeOptions with FileLog = "build.log"; Threads = 4 } {

  phony "main" (action {
    do! writeLog Message "Hello world!"

    let! opts = getCtxOptions()
    let mesg = "hello"
    printfn "Args are %A" fsi.CommandLineArgs
    printfn "Options are %A" opts
    
    // do! writeLog Message mesg -- TODO uncommenting leads to error

    return ()
    })

  rules [
    "t1" => action {do! writeLog Message "t1!"}
    "t2" => action {do! writeLog Message "t2!"}
    "t3" => action {do! writeLog Message "t3!"}
    "t4" => action {do! writeLog Message "t4!"}
  ]

}
