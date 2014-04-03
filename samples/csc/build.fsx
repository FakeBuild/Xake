// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake
open System

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

"a.exe" *> fun exe -> rule {
  let cs = exe -<.> "cs"
  do! Csc {
    CscSettings with
      Out = exe
      Src = FileList [&"a.cs"]}
}

"*.exe" *> fun exe -> rule {

  do log Level.Info "Building %s" (fullname exe)
  do! Async.Sleep(Random().Next(1500, 2500)) // simulate long operation
//  [1..1000000] |> List.map (float >> System.Math.Sqrt) |> ignore
//  do! Async.Sleep(500) // simulate long operation
  do! Csc {
    CscSettings with
      Out = exe
      Src = ls "*.cs"
    }
}

printfn "Building main"
run ([1..20] |> List.map (sprintf "a%i.exe")) |> ignore

