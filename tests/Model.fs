namespace tests

module Types =
  open System.IO

  type Rule =
    | File
    | Build of Async<unit>

  type Artifact = Artifact of FileInfo * Rule
  type FileSet = Files of Artifact list

  open System.Threading.Tasks
  type Deferred<'t>(a:Async<'t>) =
    let mutable task:Task<'t> = null
    member this.Wait =
      if (task = null) then
        task <- Async.StartAsTask(a)
      Async.AwaitTask task

module Actions =

  open Types
  open System.IO

  let fileset pattern =
    let dirIndo = DirectoryInfo (Path.GetDirectoryName pattern)
    let mask = Path.GetFileName pattern
    FileSet.Files (Seq.map (fun f -> Artifact (f,Types.Rule.File)) (dirIndo.EnumerateFiles mask) |> List.ofSeq)
