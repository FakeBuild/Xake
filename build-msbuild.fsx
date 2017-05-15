// xake build file body
#r @"packages/Xake/tools/Xake.Core.dll"

open Xake
open Xake.SystemTasks

let runclr cmd args = system (useClr >> checkErrorLevel) cmd args |> Recipe.Ignore

let build target = action {
    do! alwaysRerun()
    do! MSBuild {MSBuildSettings with BuildFile = "xake.sln"; Property = [("Configuration", "Release")]; Target = [target]}
}

do xake {ExecOptions.Default with ConLogLevel = Verbosity.Chatty } {

    filelog "build.log" Verbosity.Diag
    rules [
        "main"  => action {
            do! need ["get-deps"]
            do! need ["build"]
            do! need ["test"]
            }

        "build" => (build "Build")
        "clean" => (build "Clean")

        "get-deps" => action {
            do! runclr ".paket/paket.bootstrapper.exe" []
            do! runclr ".paket/paket.exe" ["install"]
        }

        "test" => runclr "packages/NUnit.Runners/tools/nunit-console.exe" ["./bin/XakeLibTests.dll"]
    ]

}
