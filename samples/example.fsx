#r @"../bin/Xake.Core.dll"
open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

do xakeScript {
    rules [
        "main" <== ["hw.exe"]
        "hw.dll" ..> csc {src !! "util.cs"}

        "hw.exe" ..> csc {
            target TargetType.Exe
            src !! "hw.cs"
            ref !! "hw.dll"
        }

        "(name:*).exe" ..> recipe {
            let! name = getRuleMatch "name"
            do! csc {src (!!(name + ".cs") ++ "ver.cs")}
        }

        "ver.cs" ..> recipe {
            let! envver = getEnv "VER"
            let ver = envver |> function | Some x -> x | _ -> "v0.1"

            do! writeText (sprintf """// static class App {const string Ver = "%s";}""" ver)
        }
    ]
}
