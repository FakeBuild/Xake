//#r @"../../packages/Xake/tools/Xake.Core.dll" // (1)
#r "../../core/bin/Debug/net46/Xake.dll"


open Xake                          // (2)
open Xake.Tasks.Dotnet             // (2.1)

do xakeScript {                    // (3)

    rule("main" <== ["hw.exe"])    // (4)

    rule("hw.exe" ..> recipe {     // (5)
        do! Csc {
            CscSettings with
                Src = !! "hw.cs"
        }
    })

    rule (FileRule ("greet", recipe { do! trace Info "hello" }))

}