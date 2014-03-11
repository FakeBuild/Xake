namespace Xake

open System.IO

[<AutoOpen>]
module DomainTypes =

  type RuleType =
    | File
    | Build of Async<unit>

  type ArtifactType = Artifact of FileInfo * RuleType
  type FileSetType = Files of ArtifactType list

[<AutoOpen>]
module Common =

  open DomainTypes

  let fileinfo path = new FileInfo(path)
  let simplefile path = Artifact (fileinfo path,File)
  let (<<<) path steps = Artifact (fileinfo path,Build steps)
