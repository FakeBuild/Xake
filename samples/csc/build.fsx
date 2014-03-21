// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

Name "a.exe" **> fun exe -> rule {
    do logInfo "explicit rule a.exe for %s" exe.Name
    let cs = exe -<.> "cs"
    do! Csc {
      CscSettings with
        OutFile = exe
        SrcFiles = [!"a.cs"]}
  }

Glob "*.exe" **> fun exe -> rule {
    do logInfo "glob rule *.exe for %s" exe.Name
    let cs = exe -<.> "cs"
    do! Csc {
      CscSettings with
        OutFile = exe
        SrcFiles = [!"a.cs"]}
  }

Name "main" **> fun exe -> rule {
    do! need (["a";"b";"c";"d";"e"] |> List.map (fun f -> !(f + ".exe")))
  }

printfn "Building main"
run [!"main"] |> ignore

