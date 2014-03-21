// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

Name "a.exe" **> fun exe -> rule {
  let cs = exe -<.> "cs"
  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = [&"a.cs"]}
}

Glob "*.exe" ***> BuildFile (fun exe -> rule {
  let cs = exe -<.> "cs"
  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = [&"a.cs"]}
})

Name "main" **> fun _ -> rule {
  do! need (["a";"b";"c";"d";"e"] |> List.map (fun f -> &(f + ".exe")))
}

printfn "Building main"
run [ &&"main" ] |> ignore

