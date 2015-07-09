// xake build file
// boostrapping xake.core
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let file = System.IO.Path.Combine("packages", "Xake.Core.dll")
if not (System.IO.File.Exists file) then
    printf "downloading xake.core assembly..."; System.IO.Directory.CreateDirectory("packages") |> ignore
    let url = "https://github.com/OlegZee/Xake/releases/download/v0.3.1/Xake.Core.dll"
    use wc = new System.Net.WebClient() in wc.DownloadFile(url, file + "__"); System.IO.File.Move(file + "__", file)
    printfn ""

// xake build file body
#r @"packages/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake

let build target = action {
    do! alwaysRerun()
    do! MSBuild {MSBuildSettings with BuildFile = "xake.sln"; Property = [("Configuration", "Release")]; Target = [target]}
}
(*
let systemClr cmd args =
    let cmd',args' = if Xake.Env.isUnix then "mono", cmd::args else cmd,args
    in system cmd' args'
*)

do xake {XakeOptions with FileLog = "build.log"; ConLogLevel = Verbosity.Chatty } {

    rules [
        "all"  => action {
            do! need ["get-deps"]
            do! need ["build"]
            do! need ["test"]
            }

        "build" => (build "Build")
        "clean" => (build "Clean")

        "get-deps" => action {
            let! exit_code = system ".paket/paket.bootstrapper.exe" []
            let! exit_code = system ".paket/paket.exe" ["install"]

            if exit_code <> 0 then
                failwith "Failed to install packages"
        }

        "test" => action {

            let! exit_code = system "packages/NUnit.Runners/tools/nunit-console.exe" ["./bin/XakeLibTests.dll"]
            if exit_code <> 0 then
                failwith "Failed to test"
        }
    ]

}
