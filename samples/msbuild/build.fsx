// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake {XakeOptions with FileLog = "build.log" } {

  want ["main"]

  phony "main" (action {

    do! alwaysRerun()

    do! MSBuild {
    MSBuildSettings with
        BuildFile = "Hello\\Hello.sln"
        Target = ["Clean"; "Rebuild"]
      }
    })

}
