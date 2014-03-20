namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO
  open Xake.DomainTypes

  // lists the files
  let ls pattern =
    let dirIndo = DirectoryInfo (Path.GetDirectoryName pattern)
    let mask = Path.GetFileName pattern
    Files (Seq.map Artifact (dirIndo.EnumerateFiles mask) |> List.ofSeq)

  // tests if file name matches
  let private test (mask: string) file =
    let c = function
      | '*' -> ".+"
      | '.' -> "[.]"
      | '?' -> "."
      | ch -> System.String(ch,1)
    let pattern = "^" + System.String.Concat(mask.ToCharArray() |> Array.map c) + "$"
    if System.Text.RegularExpressions.Regex.Matches(file, pattern).Count > 0 then true else false
