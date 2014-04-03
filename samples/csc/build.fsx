// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

"a.exe" *> fun exe -> rule {
  let cs = exe -<.> "cs"
  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = FileList [&"a.cs"]}
}

"*.exe" *> fun exe -> rule {

  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = ls "*.cs"
    }
}

printfn "Building main"
run (["a";"b";"c";"d";"e"] |> List.map (fun f -> f + ".exe")) |> ignore

