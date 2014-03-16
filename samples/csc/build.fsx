// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

let compile exe = rule {
    let cs = exe -<.> "cs"
    do! Csc {
      CscSettings with
        OutFile = exe
        SrcFiles = [!"a.cs"]}
  }

"a.exe" **> compile
"b.exe" **> compile
"c.exe" **> compile
"d.exe" **> compile
"e.exe" **> compile

"main" **> fun exe -> rule {
    do! need (["a";"b";"c";"d";"e"] |> List.map (fun f -> !(f + ".exe")))
  }

run [!"main"] |> ignore

