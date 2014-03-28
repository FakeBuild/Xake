namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO

  /// Part of filesystem pattern
  type PatternPart =
    | FsRoot
    //| Parent
    | Disk of string
    | DirectoryMask of string
    | Recurse
    | FileMask of string

  /// Filesystem pattern
  type Pattern = Pattern of PatternPart list

  type FilesetElement = | Includes of Pattern | Excludes of Pattern

  type FilesetOptions = {FailOnError:bool; BaseDir:string option}

  // Fileset is a set of rules
  type Fileset = Fileset of FilesetOptions * FilesetElement list

  /// Defines set of files
  type FileList = FileList of FileInfo list

  /// Default fileset options
  let DefaultOptions = {FilesetOptions.BaseDir = None; FailOnError = false}

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
          //| ".." -> Parent (* works well now with Path.Combine() *)
          | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> Disk(a)
          | a -> DirectoryMask(a)

      // parse root "\" to FsRoot
      let fsroot = if dir.StartsWith("\\") || dir.StartsWith("/") then [FsRoot] else []

      List.foldBack (fun p l -> (mapPart p)::l) parts [FileMask (pattern |> Path.GetFileName)]
      |> List.append fsroot
      |> Pattern
      
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
//          | Parent ->
//            startDir |> Seq.map (fun d -> Directory.GetParent(d).FullName)
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
  let exec (Fileset (options,fileset)) =

    let startDir = match options.BaseDir with
      | None -> Directory.GetCurrentDirectory()
      | Some path -> path

    let files pattern =
      ils (Seq.ofList [startDir]) pattern
      |> Seq.map (fun f -> FileInfo f) |> List.ofSeq

    fileset |> List.collect (function
      | Includes (Pattern pat) -> files pat
      | Excludes e -> [])  // TODO implement
    |> FileList

  let matches filePattern projectRoot =
    let absPath = Path.Combine(projectRoot,filePattern)
    let (Pattern pattern) = parse absPath
    // TODO implement. basedir matters! rules are global, so projectdir (current dir) is a basedir
    // matches "src/**/*.cs" "c:\!\src\a\b\c.cs" -> true
    fun file -> false

  let (?==) p f = false

  /// Create a file set for specific file mask.
  let (!!) includes =
    Fileset (DefaultOptions, [includes |> parse |> Includes])

  /// Defines the basedir for a fileset
  let (<<<) (Fileset (opts,ps)) dir =
    Fileset ({opts with BaseDir = Some dir}, ps)

  /// Adds includes pattern to a fileset.
  let (++) (Fileset (opts,pts)) includes =
    Fileset (opts, pts @ [includes |> parse |> Includes])

  /// Adds excludes pattern to a fileset.
  let (--) (Fileset (opts,pts)) excludes =
    Fileset (opts, pts @ [excludes |> parse |> Excludes])

  (******** builder ********)
  type FilesetBuilder() =

    let combineOptions (o1:FilesetOptions) (o2:FilesetOptions) =
      {DefaultOptions with
        BaseDir = match o1.BaseDir,o2.BaseDir with
          | Some _, Some _ -> failwith "Cannot combine filesets with basedirs defined in both (not implemented)"
          | Some _, None -> o1.BaseDir
          | _ -> o2.BaseDir
        FailOnError = o1.FailOnError || o2.FailOnError}
    
    [<CustomOperation("basedir")>]
    member this.Basedir(Fileset (opts,fs),dir:string) = Fileset ({opts with BaseDir = Some dir}, fs)

    [<CustomOperation("includes")>]
    member this.Includes(fs,pattern:FilePattern) = fs ++ pattern

    [<CustomOperation("inc")>]
    member this.IncludeFileSet(fs1:Fileset, fs2:Fileset) = this.Combine(fs1, fs2)      

    [<CustomOperation("excludes")>]
    member this.Excludes(fs, pattern:FilePattern) = fs -- pattern

    [<CustomOperation("includefile")>]
    member this.IncludeFile(Fileset (opts,fs),file:FileInfo)  =
      let elements = File.ReadAllLines file.FullName |> Array.toList |> List.map (parse >> Includes) in
      Fileset (opts, fs @ elements)

    [<CustomOperation("excludefile")>]
    member this.ExcludeFile(Fileset (opts,fs),file:FileInfo) =
      let elements = File.ReadAllLines file.FullName |> Array.toList |> List.map (parse >> Excludes) in
      Fileset (opts, fs @ elements)
      // TODO filter comments, empty lines? |> Array.filter

    member this.Yield(())  = Fileset (DefaultOptions, [])
    member this.Return(pattern:Pattern)     = Fileset (DefaultOptions, [pattern |> Includes])
    member this.Return(pattern:FilePattern) = Fileset (DefaultOptions, [pattern |> parse |> Includes])

    member this.Combine(part1, part2) = let (Fileset (o1,set1),Fileset (o2, set2)) = (part1, part2) in Fileset(combineOptions o1 o2, set1 @ set2)
    member this.Delay(f) = f()
    member this.Zero() = this.Yield ( () )

    member x.Bind(Fileset (o1, c1), f) = let (Fileset (o2, c2)) = f () in Fileset(combineOptions o1 o2, c1 @ c2)
    member x.For(fs, f) = x.Bind(fs, f)
    member x.Return(a) = x.Yield(a)

  let fileset = FilesetBuilder()
