namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO
  open Xake.DomainTypes

  let create pattern =
    let dirIndo = DirectoryInfo (Path.GetDirectoryName pattern)
    let mask = Path.GetFileName pattern
    Files (Seq.map (fun f -> Artifact (f, RuleType.File)) (dirIndo.EnumerateFiles mask) |> List.ofSeq)
