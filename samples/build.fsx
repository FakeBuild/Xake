// xake build file
#r @"../bin/Xake.Core.dll"

open Xake

do xakeScript {
  filelog "build.log" Verbosity.Normal

  phony "main" (action {
    let username = "world"
    do! trace Message "Hello, %s!" username
    })

}
