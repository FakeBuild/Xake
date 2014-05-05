// xake build file

#r @"..\..\bin\Xake.Core.dll"

open Xake
open System

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

do xake {XakeOptions with FileLog = "build.log"; Threads = 4 } {

  want ([1..20] |> List.map (sprintf "a%i.exe"))
  //want ["a1.exe"; "a2.exe"; "a3.exe"; "a4.exe"; "a5.exe"; "a6.exe"; "a7.exe"]

  rule("a.exe" *> fun exe -> action {
    let cs = exe -. "cs"
    do! Csc {
      CscSettings with
        Out = exe
        Src = FileList ["a.cs"]}
  })

  addRule "*.exe" (fun exe -> action {

    do! writeLog Level.Info "Building %s" (fullname exe)
    //do! Async.Sleep(Random().Next(1500, 2500)) // simulate long operation
  //  [1..1000000] |> List.map (float >> System.Math.Sqrt) |> ignore
  //  do! Async.Sleep(500) // simulate long operation
    do! Csc {
      CscSettings with
        Out = exe
        Src = ls "*.cs"
      }
  })

  //printfn "Building main"
}
