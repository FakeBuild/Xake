#r @"../bin/Xake.Core.dll"
open Xake

do xake ExecOptions.Default {
    rules [
        "main" <== ["hw.exe"]
        "hw.dll" ..> csc {src !! "util.cs"}

        "hw.exe" ..> csc {
            target TargetType.Exe
            src !! "hw.cs"
            ref !! "hw.dll"
        }
    ]
}