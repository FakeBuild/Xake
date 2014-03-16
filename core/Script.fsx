// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Logging.fs"
#load "Types.fs"
#load "Common.fs"
#load "Core.fs"
#load "DotnetTasks.fs"

open System.IO
open Xake

System.IO.Directory.SetCurrentDirectory "C:\\!"

"main.c" **> fun x -> rule {
  
  execstate.Post (Reset)
  do! need [!"1"; !"2"; !"3"]

  let [text1;text2;text3] = List.map (readtext) [!"1"; !"2"; !"3"]

  File.WriteAllText (x.FullName, "file1\r\n" + text1 + "\n\rfile2\r\n" + text2 + "\r\nfile3\r\n" + text3)
  }

"2" *> rule {
  do! Async.Sleep(3010)
  do! cmd "ping www.ru" |> Async.Ignore
  }

"1" *> rule {
  do! Async.Sleep(3000)
  }

"3" **> fun r -> rule {
  do! need [!"1"]

  File.WriteAllText (r.FullName, "==== file3 ====\r\n" + (readtext !"1") + "\r\n========")
  do! Async.Sleep(2000)
  }

runSync !"main.c" |> ignore

