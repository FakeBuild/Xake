namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO

  module internal Impl =

    open System.Text.RegularExpressions

    type PathPartType =
      | RootPart
      | DiskPart of string
      | DirectoryPart of string
      | Recurse 
      | FilePart of string

    /// Converts Ant -style file pattern to a list of parts
    let parsePattern pattern =
      let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)

      let dir = pattern |> Path.GetDirectoryName
      let parts = Array.toList <| dir.Split([|'\\';'/'|], System.StringSplitOptions.RemoveEmptyEntries)
      let mapPart = function
          | "**" -> Recurse
          | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> DiskPart(a)
          | a -> DirectoryPart(a)

      // TODO parse root "\" to RootPart
      List.foldBack (fun p l -> (mapPart p)::l) parts [FilePart (pattern |> Path.GetFileName)]
      
    /// Builds the regexp for testing file part
    let fileMatchRegex (pattern:string) =
      let c2r = function
        | '*' -> ".+"
        | '.' -> "[.]"
        | '?' -> "."
        | ch -> System.String(ch,1)
      let  pat = (pattern.ToCharArray() |> Array.map c2r |> System.String.Concat)
      Regex(@"^" + pat + "$", RegexOptions.Compiled + RegexOptions.IgnoreCase)  // TODO ignore case is optional (system-dependent)

    /// Recursively applied the pattern rules to every item is start list
    let rec ils (startDir:seq<string>) = function
      | [] -> startDir
      | some :: rest ->
        let data =
          match some with
          | DiskPart d ->
            seq {yield d + "\\"}
          | RootPart ->
            startDir |> Seq.map Directory.GetDirectoryRoot
          | Recurse ->
            startDir |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
          | DirectoryPart d ->
            let isMask = d.IndexOfAny([|'*';'?'|]) >= 0
            seq {
              for start in startDir do
                let path = Path.Combine(start, d)
                if not isMask && Directory.Exists(path) then
                  yield path
                else
                  yield! Directory.EnumerateDirectories(start, d, SearchOption.TopDirectoryOnly)
            }
          | FilePart mask ->
            let isMask = mask.IndexOfAny([|'*';'?'|]) >= 0
            seq {
              for start in startDir do
                let path = Path.Combine(start, mask)
                if not isMask && File.Exists(path) then
                  yield path
                else
                  yield! Directory.EnumerateFiles(start, mask)
            }
        ils data rest
  // end of module Impl

  open Impl

    /// Defines set of files
  type FileSetType = Files of FileInfo list

  // lists the files
  let ls (pattern:FilePattern) =

    let startDir = Directory.GetCurrentDirectory()
    let files = ils (Seq.ofList [startDir]) (parsePattern pattern)
    files |> Seq.map (fun f -> FileInfo f) |> List.ofSeq |> Files

  /// Gets the artifact file name
  let fullname (Artifact file) = file.FullName

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  let (?==) p f = false

