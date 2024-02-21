#r "nuget: Xake, 1.1.4.427-beta"
#r "nuget: Xake.Dotnet, 1.1.4.7-beta" (1)


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

    rule (FileRule ("greet", recipe { do! trace Info "hello" }))

}