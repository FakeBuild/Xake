namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type ArtifactType = Artifact of FileInfo

  type RuleSelectorType =
    | Regexp of string
    | Glob of string

  type BuildActionType = BuildAction of (FileInfo -> Async<unit>)

  type RuleType = Rule of RuleSelectorType * BuildActionType

  type FileSetType = Files of ArtifactType list
