namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type Artifact = FileInfo 

  type FilePattern = string

  type BuildActionType =
    | BuildAction of (Artifact -> Async<unit>)
    | BuildFile of (FileInfo -> Async<unit>)

  type RuleType = Rule of FilePattern * BuildActionType
