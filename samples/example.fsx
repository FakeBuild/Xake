// #r @"../bin/Xake.Core.dll"
#r "../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

let asyncFn a = async {
    return a + 3
}

do xakeScript {
    rules [
        "main" <== ["hw.exe"]
        "hw.dll" ..> csc {src !! "util.cs"}

        "hw.exe" ..> csc {
            target TargetType.Exe
            src !! "hw.cs"
            ref !! "hw.dll"
        }

        "app.exe" ..> recipe {
            do! need ["ver.cs"; "app.cs"]

            let! name = getTargetFile()
            do! csc {src (!!(name.FullName + ".cs") ++ "ver.cs")}

            let! opts = getCtxOptions()
            let rootPath = opts.ProjectRoot

            let! isDebug = getVar "DEBUG" |> Recipe.map (Option.defaultValue "0")

            let! result = asyncFn 3
            return ()
        }

        "(name:*).exe" ..> recipe {
            let! name = getRuleMatch "name"
            do! csc {src (!!(name + ".cs") ++ "ver.cs")}
        }

        "ver.cs" ..> recipe {
            let! envver = getEnv "VER"
            let ver = envver |> Option.defaultValue "v0.1"
            do! writeText (sprintf """// static class App {const string Ver = "%s";}""" ver)
        }
    ]
}
