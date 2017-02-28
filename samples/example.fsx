#r @"../bin/Xake.Core.dll"
open Xake

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
            do! csc {src !!(name + ".cs")}
        }
    ]
}
