// xake build file
// #r @"../bin/Debug/Xake.Core.dll"
#r "../core/bin/Debug/net46/Xake.dll"

open Xake

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  phony "main" (action {
    do! trace Message "Hello world!"

    let! opts = getCtxOptions()
    let mesg = "hello"
    printfn "Args are %A" fsi.CommandLineArgs
    printfn "Options are %A" opts
    
    do! trace Message "dfdf"

    return ()
    })

  rules [
    "t1" => action {do! trace Message "t1!"}
    "t2" => action {do! trace Message "t2!"}
    "t3" => action {do! trace Message "t3!"}
    "t4" => action {do! trace Message "t4!"}
  ]

}
