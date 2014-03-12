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
    | Run of FileInfo * RuleType * AsyncReplyChannel<Task<FileInfo>>
    | Reset
    | GetTask of FileInfo * AsyncReplyChannel<Task<FileInfo> option>

  let execstate = MailboxProcessor.Start(fun mbox ->
    let rec loop(map) = async {
      let! msg = mbox.Receive()
      match msg with
      | Reset ->        
        // TODO cancel pending tasks
        // map |> Map.iter (fun file task -> ...)
        return! loop(Map.empty)

      | Run(file,rule,chnl) ->

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

          logInfo "started build '%s'" file.FullName
          chnl.Reply(task)
          return! loop(Map.add file.FullName task map)

      | GetTask(file,chnl) ->        
        chnl.Reply (map |> Map.tryFind file.FullName)
        return! loop(map)
    }
    loop(Map.empty) )

  // executes single artifact
  let exec (Artifact (file,rule)) =

    match rule with
    | File -> Async.FromContinuations (fun (cont,_,_) -> cont(file))
    | _ -> async {      
      let! task = execstate.PostAndAsyncReply(fun chnl -> Run(file, rule, chnl))
      return! Async.AwaitTask task
      }

  let execMany = Seq.ofArray >> Seq.map exec >> Async.Parallel
  // executes
  let need artifacts = artifacts |> Seq.map exec |> Seq.toArray |> Async.Parallel |> Async.Ignore

  // Runs the artifact synchronously
  let runSync = function
    | (Artifact (file,Build rule)) ->
      Async.RunSynchronously rule
      file
    | _ -> failwith "Expected artifact with rule"

  let mutable private artifacts = Map.empty

  // creates new artifact rule
  let ( *> ) path steps : unit =
    let fullname = fileinfo path
    let artifact = Artifact (fullname,Build steps)
    artifacts <- Map.add fullname.FullName artifact artifacts

  // creates new artifact rule
  let ( **> ) path fnsteps : unit =
    let fullname = fileinfo path
    let artifact = Artifact (fullname,Build (fnsteps fullname))
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
