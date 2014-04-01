namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type Artifact = FileInfo 
  type BuildAction = BuildAction of (Artifact -> Async<unit>)
