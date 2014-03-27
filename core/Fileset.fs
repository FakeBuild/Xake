namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO

  /// Part of filesystem pattern
  type PatternPart =
    | FsRoot
    | Disk of string
    | DirectoryMask of string
    | Recurse
    | FileMask of string

  /// Filesystem pattern
  type Pattern = Pattern of PatternPart list

  /// Fileset element
  type Element =
    | Includes of Pattern
    | Excludes of Pattern
//    | IncludeFileSet of Fileset
//    (* I do not want exclude of excludes here so no ExcludeFileset *)
//    | IncludeFile of FileInfo
//    | ExcludeFile of FileInfo

  // Fileset is a set of rules
  and Fileset = Fileset of Element list

    /// Defines set of files
  type FileList = FileList of FileInfo list

  /// Implementation module
  module internal Impl =

    open System.Text.RegularExpressions

    /// Converts Ant -style file pattern to a list of parts
    let parse pattern =
      let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)

      let dir = pattern |> Path.GetDirectoryName
      let parts = Array.toList <| dir.Split([|'\\';'/'|], System.StringSplitOptions.RemoveEmptyEntries)
      let mapPart = function
          | "**" -> Recurse
          | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> Disk(a)
          | a -> DirectoryMask(a)

      // TODO parse root "\" to FsRoot
      List.foldBack (fun p l -> (mapPart p)::l) parts [FileMask (pattern |> Path.GetFileName)] |> Pattern
      
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
          | Disk d ->
            seq {yield d + "\\"}
          | FsRoot ->
            startDir |> Seq.map Directory.GetDirectoryRoot
          | Recurse ->
            startDir |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
          | DirectoryMask d ->
            let isMask = d.IndexOfAny([|'*';'?'|]) >= 0
            seq {
              for start in startDir do
                let path = Path.Combine(start, d)
                if not isMask && Directory.Exists(path) then
                  yield path
                else
                  yield! Directory.EnumerateDirectories(start, d, SearchOption.TopDirectoryOnly)
            }
          | FileMask mask ->
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

  // lists the files
  let ls (filePattern:FilePattern) =
    let (Pattern pattern) = parse filePattern
    let startDir = Directory.GetCurrentDirectory()
    let files = ils (Seq.ofList [startDir]) pattern
    files |> Seq.map (fun f -> FileInfo f) |> List.ofSeq |> FileList

  /// Gets the artifact file name
  let fullname (Artifact file) = file.FullName

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  /// Draft implementation of fileset execute
  let exec (Fileset fileset) =
    let startDir = Directory.GetCurrentDirectory()
    let files pattern =
      ils (Seq.ofList [startDir]) pattern
      |> Seq.map (fun f -> FileInfo f) |> List.ofSeq
    fileset |> List.collect (function
      | Includes (Pattern pat) -> files pat
      | Excludes e -> [])  // TODO implement
    |> FileList

  let (?==) p f = false

  (******** builder ********)
  type FilesetBuilder() =
    
    [<CustomOperation("includes")>]
    member this.Includes(Fileset fs,pattern:FilePattern) = fs @ [pattern |> parse |> Includes] |> Fileset

    [<CustomOperation("inc")>]
    member this.IncludeFileSet(Fileset fs, fs2) = let (Fileset set2) = fs2 in fs @ set2 |> Fileset

    [<CustomOperation("excludes")>]
    member this.Excludes(pattern:FilePattern) = Fileset [pattern |> parse |> Excludes]

    [<CustomOperation("includefile")>]
    member this.IncludeFile(file:FileInfo)  = File.ReadAllLines file.FullName |> Array.toList |> List.map (parse >> Includes) |> Fileset

    [<CustomOperation("excludefile")>]
    member this.ExcludeFile(file:FileInfo)  = File.ReadAllLines file.FullName |> Array.toList |> List.map (parse >> Excludes) |> Fileset
      // TODO filter comments, empty lines? |> Array.filter

//    member this.Yield(pattern:Pattern)      = Fileset [pattern |> Includes]
//    member this.Yield(pattern:string)  = Fileset [pattern |> parse |> Includes]
    member this.Yield(())  = Fileset []
    member this.Return(pattern:Pattern)     = Fileset [pattern |> Includes]
    member this.Return(pattern:FilePattern) = Fileset [pattern |> parse |> Includes]

    member this.Combine(part1, part2) = let (Fileset set1,Fileset set2) = (part1, part2) in List.concat [set1; set2] |> Fileset
    member this.Delay(f) = f()
    member this.Zero() = Fileset []

    member x.Bind(Fileset c1, f) = let (Fileset c2) = f () in Fileset(c1 @ c2)
    member x.For(fs, f) = x.Bind(fs, f)
    member x.Return(a) = x.Yield(a)

  let fileset = FilesetBuilder()
