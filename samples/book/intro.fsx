#r "paket:
    nuget Xake ~> 1.1 prerelease
    nuget Xake.Dotnet ~> 1.1 prerelease //" // (1)

open Xake                          // (2)
open Xake.Dotnet                   // (2.1)

do xakeScript {                    // (3)
    rule("main" <== ["hw.exe"])    // (4)
    rule("hw.exe" ..> recipe {     // (5)
        do! Csc {
            CscSettings with
                Src = !! "hw.cs"
        }
    })

    rule (PhonyRule ("greet", trace Info "hello"))
}