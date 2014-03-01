// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Model.fs"
open System.Threading.Tasks
open System.IO

open tests
open tests.Types

// Define your library scripting code here
let files = Actions.FileSet "..\*.*"
let names = Seq.map (fun (File f) -> f.Name) files |> Seq.toArray

let log (s:string) =
  System.Console.Out.WriteLine(s) |> ignore
  System.Console.Out.Flush

let task a = Rule a

//let task a = Async.StartAsTask(a)

let wait = function
  | File f -> Async.FromContinuations (fun (cont,_e,_c) -> cont(f))
  | Rule r -> r

let target0 = File (new FileInfo("c:\\!\\1"))

let target2 = Rule (async {
  log "making file2..."
  do! Async.Sleep(3000)
  log "done file2..."
  return new FileInfo("c:\\!\\2")
  })

let target1 = Rule (async {
  log "making file1..."
  
  do! Async.Sleep(1000)
  log "done file1..."
  return new FileInfo("c:\\!\\1")
  })

let target3 = Rule (async {
  log "making file3..."
  let! file1 = wait target1
  let result = new FileInfo("c:\\!\\3")

  File.WriteAllText (result.FullName, "==== file3 ====\r\n" + File.ReadAllText(file1.FullName) + "\r\n========")
  do! Async.Sleep(2000)
  log "done file3..."
  return result
  })

let main = Rule (async {
  log "start main"
  let! file1 = wait target1
  let! file2 = wait target2
  let! file3 = wait target3

  let text1 = File.ReadAllText(file1.FullName)
  let text2 = File.ReadAllText(file2.FullName)
  let text3 = File.ReadAllText(file3.FullName)

  let file3 = new FileInfo("c:\\!\main.c")
  File.WriteAllText (file3.FullName, "file1\r\n" + text1 + "\n\rfile2\r\n" + text2 + "\r\nfile3\r\n" + text3)

  return file3
  })

Async.RunSynchronously(wait main)
//let compileTask = task (fun () ->
//  Control.Async.Sleep 1000 |> ignore
//  new FileInfo("c:\\1"))
//


let t1 = new System.Threading.Tasks.Task<int>(fun () -> 
  printf "running task"
  123)

t1.AsyncState
t1.Result
t1.Start
Async.AwaitTask t1

let file = wait (File (new FileInfo("c:\\!\\1")))
let a = async {
  do! Async.Sleep(100)
  printf "a task"
  return 123
  }

let b = async {
  let! ares = a
  let! file = file
  do! Async.Sleep(100)
  printf "b task"
  return 123
  }

Async.RunSynchronously b


