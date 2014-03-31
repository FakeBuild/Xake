namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type Artifact = FileInfo 
  type FilePattern = string
  type BuildAction = BuildAction of (Artifact -> Async<unit>)
