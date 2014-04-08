module Tasks =

  open System
  open System.Threading.Tasks

  let t = new Task<string> (Func<string> (fun () -> "Hello"))
  let onComplete = async {
    printfn "waiting task"
    let! a = Async.AwaitTask t
    printfn "task complete with result: %A" a
  }

  Async.Start onComplete
  t.Start()
