namespace Xake

open Xake.Logging

[<AutoOpen>]
module Types =
  open System.IO

  type Rule =
    | File
    | Build of Async<unit>

  type Artifact = Artifact of FileInfo * Rule
  type FileSet = Files of Artifact list

[<AutoOpen>]
module Fileset =

  open Types
  open System.IO

  let fileset pattern =
    let dirIndo = DirectoryInfo (Path.GetDirectoryName pattern)
    let mask = Path.GetFileName pattern
    FileSet.Files (Seq.map (fun f -> Artifact (f,Types.Rule.File)) (dirIndo.EnumerateFiles mask) |> List.ofSeq)

[<AutoOpen>]
module Build =

  open System
  open System.IO
  open Types

  // builds 
  let private run (Artifact (file,rule)) =
    match rule with
    | File -> Async.FromContinuations (fun (cont,_e,_c) -> cont(file))
    | Build r -> async {
      do! r
      return file
    }
  let private runMany = Seq.ofArray >> Seq.map run >> Async.Parallel 

  let fileinfo path = new FileInfo(path)
  let simplefile path = Artifact (fileinfo path,File)
  let (<<<) path steps = Artifact (fileinfo path,Build steps)

  // execution context
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
  let (<<) path steps : unit =
    let fullname = fileinfo path
    let artifact = Artifact (fullname,Build steps)
    artifacts <- Map.add fullname.FullName artifact artifacts

  let (!) path =
    let fullname = fileinfo path
    match Map.tryFind fullname.FullName artifacts with
    | Some a -> a
    | None ->
      match fullname.Exists with
        | true -> Artifact (fullname,File)
        | _ -> failwithf "Artifact '%s': neither file nor rule found" fullname.FullName

  let rule = async

[<AutoOpen>]
module DotNetTasks =

  open Types
  open System.IO

  type TargetType = |Exe |Dll
  type CscSettingsType = {Target: TargetType; OutFile: FileInfo; SrcFiles: Artifact list}
  let CscSettings = {Target = Exe; OutFile = null; SrcFiles = []}

  let Csc settings = 
    async {
      // TODO call compiler
      do logInfo "Compiling %s" settings.OutFile.FullName
    }
