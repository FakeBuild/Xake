namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type ArtifactType =
    | FileArtifact of FileInfo 
    | JustArtifact of string

  type RuleSelectorType =
    | Regexp of string
    | Glob of string
    | Name of string

  type BuildActionType =
    | BuildAction of (ArtifactType -> Async<unit>)
    | BuildFile of (FileInfo -> Async<unit>)

  type RuleType = Rule of RuleSelectorType * BuildActionType

  type FileSetType = Files of ArtifactType list
