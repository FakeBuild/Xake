// xake build file
// boostrapping xake.core
open System.IO
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let file = Path.Combine("packages", "Xake.Core.dll")
if not (File.Exists file) then
    printf "downloading xake.core assembly..."; Directory.CreateDirectory("packages") |> ignore
    let url = "https://github.com/OlegZee/Xake/releases/download/v0.7.0/Xake.Core.dll"
    use wc = new System.Net.WebClient() in wc.DownloadFile(url, file + "__"); File.Move(file + "__", file)
    printfn ""

// xake build file body
#r @"packages/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake
open Xake.SystemTasks

let runclr cmd args = system (useClr >> checkErrorLevel) cmd args |> Action.Ignore

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
