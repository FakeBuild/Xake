namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type ArtifactType = Artifact of FileInfo 

  type FilePattern = string

  type BuildActionType =
    | BuildAction of (ArtifactType -> Async<unit>)
    | BuildFile of (FileInfo -> Async<unit>)

  type RuleType = Rule of FilePattern * BuildActionType

  type FileSetType = Files of ArtifactType list
