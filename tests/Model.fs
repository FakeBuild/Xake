namespace tests

module Types =
  open System.IO
  open System.Threading.Tasks

  type Rule = (unit) -> FileInfo

  type Artifact =
    | File of FileInfo
    | Rule of Task<FileInfo>

  type Deferred<'t>(a:Async<'t>) =
    let mutable task:Task<'t> = null
    member this.Wait =
      if (task = null) then
        task <- Async.StartAsTask(a)
      Async.AwaitTask task
    
module Actions =

  open System.IO

  let FileSet pattern =
    let dirIndo = DirectoryInfo (System.IO.Path.GetDirectoryName pattern)
    let mask = System.IO.Path.GetFileName pattern
    Seq.map Types.File (dirIndo.EnumerateFiles mask)

