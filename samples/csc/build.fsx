// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

"a.exe" *> fun exe -> rule {
  let cs = exe -<.> "cs"
  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = [&"a.cs"]}
}

"*.exe" *> fun exe -> rule {

  let srcFiles = scan (fileset {includes "*.cs"})
  do! need srcFiles
  
  do! Csc {
    CscSettings with
      OutFile = exe
      SrcFiles = srcFiles}
}

printfn "Building main"
run (["a";"b";"c";"d";"e"] |> List.map (fun f -> f + ".exe")) |> ignore

