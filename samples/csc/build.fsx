// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

Glob "*.exe" **> fun exe -> rule {
    let cs = exe -<.> "cs"
    do! Csc {
      CscSettings with
        OutFile = exe
        SrcFiles = [!"a.cs"]}
  }

Glob "main" **> fun exe -> rule {
    do! need (["a";"b";"c";"d";"e"] |> List.map (fun f -> !(f + ".exe")))
  }

run [!"main"] |> ignore

