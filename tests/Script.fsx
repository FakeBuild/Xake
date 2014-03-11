// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Logging.fs"
#load "Types.fs"
#load "Core.fs"
#load "DotnetTasks.fs"
#load "Fileset.fs"

open System.IO

open Xake
open Xake.Logging
open Xake.Common
open Xake.Core
open Xake.DotnetTasks

System.IO.Directory.SetCurrentDirectory "C:\\!"

"main.c" << rule {
  context <- Map.empty
  let! [|file1;file2;file3|] = execMany [|!"1"; !"2"; !"3"|]

  let text1 = File.ReadAllText(file1.FullName)
  let text2 = File.ReadAllText(file2.FullName)
  let text3 = File.ReadAllText(file3.FullName)

  let file3 = fileinfo "main.c"
  File.WriteAllText (file3.FullName, "file1\r\n" + text1 + "\n\rfile2\r\n" + text2 + "\r\nfile3\r\n" + text3)
  }

"2" << rule {
  do! Async.Sleep(3000)
  }

"1" << rule {
  do! Async.Sleep(3000)
  }

"3" << rule {
  let! file1 = exec !"1"
  let result = fileinfo "3"

  File.WriteAllText (result.FullName, "==== file3 ====\r\n" + File.ReadAllText(file1.FullName) + "\r\n========")
  do! Async.Sleep(2000)
  }

"a.exe" << Csc
  {
    CscSettings with
      OutFile = fileinfo "a.exe"
      SrcFiles = [!"a.cs"]
  }


logInfo ">>running from thread# %i" System.Threading.Thread.CurrentThread.ManagedThreadId
runSync !"main.c"


// Define your library scripting code here
let files = Fileset.create "..\*.*"

let names fileset =
 let (Files ff) = fileset
 ff |> List.map (fun (Artifact (f,_)) -> f.Name) |> List.toArray
