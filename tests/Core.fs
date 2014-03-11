namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading.Tasks

  open Xake.DomainTypes
  open Xake.Common

  open Xake.Logging

  // execution context
  type ExecMessage =
    | Run of ArtifactType * AsyncReplyChannel<Task<FileInfo>>
    | GetTask of FileInfo * AsyncReplyChannel<Task<FileInfo> option>

  let execstate = MailboxProcessor.Start(fun mbox ->
    let rec loop(map) = async {
      let! msg = mbox.Receive()
      match msg with
      | Run(Artifact (file,rule),chnl) ->

        let task = Map.tryFind file.FullName map

        match task,rule with
        | Some task, _-> 
          chnl.Reply(task)
          return! loop(map)

        | None,File -> failwith "Adding file '%s' to exec state is not allowed" file
        | None,Build r ->
          let task = Async.StartAsTask (async {
            do! r
            do logInfo "completed task '%s'" file.FullName
            return file
          })


          logInfo ">>added task %s from thread# %i" file.FullName System.Threading.Thread.CurrentThread.ManagedThreadId
          logInfo "started '%s'" file.FullName
          chnl.Reply(task)
          return! loop(Map.add file.FullName task map)

      | GetTask(file,chnl) ->
        
        chnl.Reply (map |> Map.tryFind file.FullName)

        return! loop(map)
    }
    loop(Map.empty) )

  // get the async computation
  let private run (Artifact (file,rule)) =
    match rule with
    | File -> Async.FromContinuations (fun (cont,_e,_c) -> cont(file))
    | Build r -> async {
      do! r
      return file
    }
  let private runMany = Seq.ofArray >> Seq.map run >> Async.Parallel 

  let mutable context = Map.empty

  let exec (Artifact (file,rule)) =

    let task = context |> Map.tryFind file.FullName

    match task,rule with
    | Some task, _-> async {
        do! Async.AwaitTask task
        return file
      }
    | None,File -> Async.FromContinuations (fun (cont,_,_) -> cont(file))
    | None,Build r ->
      let task = Async.StartAsTask r
      context <- Map.add file.FullName task context
      logInfo ">>added task %s from thread# %i" file.FullName System.Threading.Thread.CurrentThread.ManagedThreadId

      logInfo "started '%s'" file.FullName
      async {
        do! Async.AwaitTask task
        do logInfo "completed task '%s'" file.FullName
        return file
      }

  let execMany = Seq.ofArray >> Seq.map exec >> Async.Parallel 

  // entry point, runs synchronously
  let runSync = run >> Async.RunSynchronously

  let mutable private artifacts = Map.empty

  // creates new artifact rule
  let (<<) path steps : unit =
    let fullname = fileinfo path
    let artifact = Artifact (fullname,Build steps)
    artifacts <- Map.add fullname.FullName artifact artifacts

  // creates new file artifact
  let (!) path =
    let fullname = fileinfo path
    match Map.tryFind fullname.FullName artifacts with
    | Some a -> a
    | None ->
      match fullname.Exists with
        | true -> Artifact (fullname, RuleType.File)
        | _ -> failwithf "Artifact '%s': neither file nor rule found" fullname.FullName

  let rule = async
