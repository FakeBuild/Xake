namespace Xake

[<AutoOpen>]
module DomainTypes =

  type Artifact = System.IO.FileInfo 
  type BuildAction = BuildAction of (Artifact -> Async<unit>)
