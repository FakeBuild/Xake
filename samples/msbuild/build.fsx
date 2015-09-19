// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake

do xake ExecOptions.Default {

  want ["build\\Hello.exe"]

  rule ("build\\Hello.exe" *> fun _ -> action {

    do! MSBuild {
    MSBuildSettings with
        BuildFile = "Hello\\Hello.sln"
        Target = ["Clean"; "Rebuild"]
        Property = [("OutDir", "..\\build")]
      }
  })
}
