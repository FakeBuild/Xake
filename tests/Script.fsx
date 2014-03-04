// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "Model.fs"
open System.Threading.Tasks
open System.IO

open tests
open tests.Types

// Define your library scripting code here
let files = Actions.fileset "..\*.*"

let (Files ff) = files
let names = List.map (fun (Artifact (f,_)) -> f.Name) ff |> List.toArray

let log (s:string) =
  System.Console.Out.WriteLine(s) |> ignore
  System.Console.Out.Flush |> ignore

let run (Artifact (file,rule)) =
  match rule with
  | File -> Async.FromContinuations (fun (cont,_e,_c) -> cont(file))
  | Build r -> async {
    do! r
    return file
  }
let runMany = Seq.ofArray >> Seq.map run >> Async.Parallel 
  
// execution context
let mutable context = Map.empty

let exec (Artifact (file,rule)) =
  let task = context |> Map.tryFind file.FullName

  match task,rule with
  | Some task, _->
    async {
      do! Async.AwaitTask task
      return file
    }
  | None,File -> Async.FromContinuations (fun (cont,_,_) -> cont(file))
  | None,Build r ->
    let task = Async.StartAsTask r
    context <- Map.add file.FullName task context
    log (">>added task " + file.FullName + " from thread#" + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString())
    async {
      do! Async.AwaitTask task
      return file
    }

let execMany = Seq.ofArray >> Seq.map exec >> Async.Parallel 

let fileinfo path = new FileInfo(path)
let simplefile path = Artifact (fileinfo path,File)
let (<<<) path steps = Artifact (fileinfo path,Build steps)

let target0 = simplefile "c:\\!\\1"

let target2 = "c:\\!\\2" <<< async {
  do log "making file2..."
  do! Async.Sleep(3000)
  log "done file2..." |> ignore
  }

let target1 = "c:\\!\\1" <<< async {
  log "making file1..."  
  do! Async.Sleep(3000)
  log "done file1..."
  }

let target3 = "c:\\!\\3" <<< async {
  log "making file3..."
  let! file1 = exec target1
  let result = fileinfo "c:\\!\\3"

  File.WriteAllText (result.FullName, "==== file3 ====\r\n" + File.ReadAllText(file1.FullName) + "\r\n========")
  do! Async.Sleep(2000)
  log "done file3..."
  }

let main = "c:\\!\main.c" <<< async {
  log "start main"
  context <- Map.empty
  let! [|file1;file2;file3|] = execMany [|target1; target2; target3|]

  let text1 = File.ReadAllText(file1.FullName)
  let text2 = File.ReadAllText(file2.FullName)
  let text3 = File.ReadAllText(file3.FullName)

  let file3 = fileinfo "c:\\!\main.c"
  File.WriteAllText (file3.FullName, "file1\r\n" + text1 + "\n\rfile2\r\n" + text2 + "\r\nfile3\r\n" + text3)
  }

log (">>running from thread#" + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString())
Async.RunSynchronously(run main)
