namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading.Tasks

  open Xake

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
            do logInfo "completed build '%s'" file.FullName
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

  // gets the 
  let fullname (Artifact (file,_)) = file.FullName

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  // turns the file into Artifact type
  let simplefile path = Artifact (FileInfo(path),File)

  // executes single artifact
  let private execOne (Artifact (file,rule)) =

    match rule with
    | File -> Async.FromContinuations (fun (cont,_,_) -> cont(file))
    | _ -> async {      
      let! task = execstate.PostAndAsyncReply(fun chnl -> Run(file, rule, chnl))
      return! Async.AwaitTask task
      }

  let exec = Seq.ofList >> Seq.map execOne >> Async.Parallel
  let need artifacts = artifacts |> exec |> Async.Ignore

  // Runs the artifact synchronously
  let runSync = function
    | (Artifact (file,Build rule)) ->
      Async.RunSynchronously rule
      file
    | _ -> failwith "Expected artifact with rule"

  // runs execution of all artifact rules in parallel
  let run (artifacts: ArtifactType list)= 
    let runOne = function
      | (Artifact (file,Build rule)) -> (file, rule)
      | _ -> failwith "Expected artifact with rule"
    let rules = List.map (runOne >> snd) artifacts |> Seq.ofList |> Async.Parallel
    Async.RunSynchronously rules |> ignore

    List.map (runOne >> fst) artifacts

  let mutable private artifacts = Map.empty

  // creates new artifact rule
  let ( **> ) path fnsteps : unit =
    let file = FileInfo(path)
    let artifact = Artifact (file,Build (fnsteps file))
    artifacts <- Map.add file.FullName artifact artifacts

  let ( *> ) path steps : unit =
    path **> fun _ -> steps

  // gets an artifact for file
  let (!) path =
    let file = FileInfo(path)
    match Map.tryFind file.FullName artifacts, file.Exists with
    | Some a, _ -> a
    | None, true -> Artifact (file, RuleType.File)
    | _ -> failwithf "Artifact '%s': neither file nor rule found" file.FullName

  let rule = async

  // TODO move all three generic methods somewhere else Xake.Util?
  // generic method to turn any fn to async task
  let wait fn artifact = async {
    do! need [artifact]
    return artifact |> fn
    }

  // executes the fn on all artifacts
  let allf fn aa = async {
    let! results = aa |> (List.map fn) |> List.toSeq |> Async.Parallel
    return results |> Array.toList
    }

  let all aa =
    let identity i = i
    allf identity aa
