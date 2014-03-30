namespace Xake

[<AutoOpen>]
module Core =

  open System.IO
  open System.Threading.Tasks

  open Xake

  // execution context
  type ExecMessage =
    | Run of Artifact * BuildActionType * AsyncReplyChannel<Task<FileInfo>>
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

      | Run(artifact, BuildAction action,chnl) ->
        
        let fullname = fullname artifact
        let task = map |> Map.tryFind fullname

        match task with
        | Some task -> 
          chnl.Reply(task)
          return! loop(map)

        | None ->
          let task = Async.StartAsTask (async {
            do! action artifact
            do log Level.Verbose "completed build '%s'" fullname
            return artifact
          })

          do log Level.Verbose "started build '%s'" fullname
          chnl.Reply(task)
          return! loop(Map.add fullname task map)

      | GetTask(file,chnl) ->        
        chnl.Reply (map |> Map.tryFind file.FullName)
        return! loop(map)
    }
    loop(Map.empty) )

  // TODO make a parameter
  let projectRoot = Directory.GetCurrentDirectory()
  let mutable private rules:Map<FilePattern,BuildActionType> = Map.empty

  // locates the rule
  let internal locateRule (artifact:Artifact) : Async<unit> option =
    let matchRule pattern b = 
      match Fileset.matches pattern projectRoot artifact.FullName with
        | true ->
          log Verbose "Found pattern '%s' for %s" pattern artifact.Name
          Some (b)
        | false -> None

    let mapAction = function
      | BuildAction act -> act artifact
      | BuildFile b -> b artifact
    rules |> Map.tryPick matchRule |> Option.map mapAction

  let locateRuleOrDie a =
    match locateRule a with
    | Some rule -> rule
    | None -> failwithf "Failed to locate file for '%s'" (fullname a)

  // creates new artifact rule
  let ( ***> ) selector action : unit =
    let rule = Rule (selector, action)
    rules <- Map.add selector action rules

  // creates new artifact rule
  let ( *> ) selector buildfile : unit =
    selector ***> BuildFile buildfile

  // gets an rule for file
  let ( ~& ) path :Artifact = (System.IO.FileInfo path)

  // executes single artifact
  let private execOne artifact =
    match locateRule artifact with
    | Some rule ->
      async {      
        let! task = execstate.PostAndAsyncReply(fun chnl -> Run(artifact, BuildAction (fun _ -> rule), chnl))
        return! Async.AwaitTask task
      }
    | None ->
      if not artifact.Exists then failwithf "Neither rule nor file is found for '%s'" (fullname artifact)
      Async.FromContinuations (fun (cont,_e,_c) -> cont(artifact))

  let exec = Seq.ofList >> Seq.map execOne >> Async.Parallel
  let need artifacts = artifacts |> exec |> Async.Ignore

  // Runs the artifact synchronously
  let runSync a = locateRuleOrDie a |> Async.RunSynchronously |> ignore; a

  // runs execution of all artifact rules in parallel
  let run =
    List.map locateRule >> List.filter Option.isSome >> List.map Option.get >> Seq.ofList >> Async.Parallel >> Async.RunSynchronously >> ignore

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
