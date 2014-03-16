namespace Xake

open System.IO

[<AutoOpen>]
module DomainTypes =

  type RuleType =
    | File
    | Build of Async<unit>

  type ArtifactType = Artifact of FileInfo * RuleType
  type FileSetType = Files of ArtifactType list
