// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

"a.exe" **> fun exe -> rule {
    do! need [!"a.cs"]
    do! Csc {
      CscSettings with
        OutFile = exe
        SrcFiles = [!"a.cs"]}
  }

run [!"a.exe"] |> ignore

