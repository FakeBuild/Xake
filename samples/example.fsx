#r @"../bin/Xake.Core.dll"
open Xake

do xake ExecOptions.Default {
    rules [
        "main" <== ["hw.exe"]
        "hw.dll" ..> Csc {
            CscSettings with
              Target = TargetType.Library
              Src = !! "util.cs"
            }

        "hw.exe" ..> Csc {
            CscSettings with
              Src = !! "hw.cs"
              Ref = !! "hw.dll"
            }
    ]
}