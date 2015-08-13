// xake build file

#r @"..\..\bin\Debug\Xake.Core.dll"

open Xake
open System

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
type Ll<'a> = 
  | (::) of 'a * Ll<'a>
  | Nil

let x = 1 :: 2 :: Nil

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  want ([1..20] |> List.map (sprintf "a%i.exe"))
  //want ["a1.exe"; "a2.exe"; "a3.exe"; "a4.exe"; "a5.exe"; "a6.exe"; "a7.exe"]

  rule("a.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a.cs"
      }
    })

  addRule "*.exe" (fun exe -> action {

    do! trace Level.Info "Building %s" exe.FullName
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
