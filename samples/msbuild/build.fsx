// xake build file
#r @"../../bin/Xake.Core.dll"

open Xake
open Xake.Tasks.Dotnet

do xake ExecOptions.Default {

  want ["build\\Hello.exe"]

  rule ("build\\Hello.exe" ..> MSBuild {
    MSBuildSettings with
        BuildFile = "Hello\\Hello.sln"
        Target = ["Clean"; "Rebuild"]
        Property = [("OutDir", "..\\build")]
    })
}
