namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO
  open Xake.DomainTypes

  // TODO support common notation (ant?) both in ls and matches

  // gets the artifact file name
  let fullname (Artifact file) = file.FullName

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  // tests if file name matches
  let matches (pattern:FilePattern) (file:FileInfo) =
    let regexpMatch pat = System.Text.RegularExpressions.Regex.Matches(file.FullName, pat).Count > 0
    let globToRegex (mask: string) =
      let c = function
        | '*' -> ".+"
        | '.' -> "[.]"
        | '?' -> "."
        | ch -> System.String(ch,1)
      (mask.ToCharArray() |> Array.map c |> System.String.Concat) + "$"

    regexpMatch (globToRegex pattern)

  let (?==) = matches

  // lists the files
  let ls (pattern:FilePattern) =
    let dirIndo = DirectoryInfo (Path.GetDirectoryName pattern)
    Path.GetFileName pattern |> dirIndo.EnumerateFiles |> Seq.map Artifact |> List.ofSeq |> Files

