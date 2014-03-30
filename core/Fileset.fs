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
    | Directory of string
    | Recurse
    | FileMask of string
    | FileName of string

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

    let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)
    let isMask (a:string) = a.IndexOfAny([|'*';'?'|]) >= 0
    let iif fn b c a = if fn a then b a else c a

    /// Builds the regexp for testing file part
    let fileMatchRegex (pattern:string) =
      let c2r = function
        | '*' -> ".*"
        | '.' -> "[.]"
        | '?' -> "."
        | ch -> System.String(ch,1)
      let  pat = (pattern.ToCharArray() |> Array.map c2r |> System.String.Concat)
      Regex(@"^" + pat + "$", RegexOptions.Compiled + RegexOptions.IgnoreCase)  // TODO ignore case is optional (system-dependent)

    /// Converts Ant -style file pattern to a list of parts
    let parse pattern =

      let mapPart = function
          | "**" -> Recurse
          //| ".." -> Parent (* works well now with Path.Combine() *)
          | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> Disk(a)
          | a when isMask a -> DirectoryMask(a)
          | a -> Directory(a)

      let dir = pattern |> Path.GetDirectoryName
      let parts = if dir = null then [||] else dir.Split([|'\\';'/'|], System.StringSplitOptions.RemoveEmptyEntries)

      // parse root "\" to FsRoot
      let fsroot = if dir <> null && (dir.StartsWith("\\") || dir.StartsWith("/")) then [FsRoot] else []
      let filepart = pattern |> Path.GetFileName |> (iif isMask FileMask FileName)

      fsroot @ (Array.map mapPart parts |> List.ofArray) @ [filepart]
      |> Pattern
      
    /// Recursively applied the pattern rules to every item is start list
    let listFiles =
      let applyPart (paths:#seq<string>) = function
      | Disk d          -> seq {yield d + "\\"}
      | FsRoot          -> paths |> Seq.map Directory.GetDirectoryRoot
      | Recurse         -> paths |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
      | DirectoryMask m -> paths |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, m, SearchOption.TopDirectoryOnly))
      | Directory d     -> paths |> Seq.map (fun dir -> Path.Combine(dir, d)) |> Seq.filter Directory.Exists
      | FileMask mask   -> paths |> Seq.collect (fun dir -> Directory.EnumerateFiles(dir, mask))
      | FileName f      -> paths |> Seq.map (fun dir -> Path.Combine(dir, f)) |> Seq.filter File.Exists
      in
      List.fold applyPart

    // combines two fileset options
    let combineOptions (o1:FilesetOptions) (o2:FilesetOptions) =
      {DefaultOptions with
        BaseDir =
          match o1.BaseDir,o2.BaseDir with
          | Some _, Some _ -> failwith "Cannot combine filesets with basedirs defined in both (not implemented)"
          | Some _, None -> o1.BaseDir
          | _ -> o2.BaseDir
        FailOnError = o1.FailOnError || o2.FailOnError}

    // combines two filesets
    let combineWith (Fileset (o2, set2)) (Fileset (o1,set1)) =
      Fileset(combineOptions o1 o2, set1 @ set2)

    // Combines result of reading file to a fileset
    let combineWithFile map (file:FileInfo) (Fileset (opts,fs)) =
      let elements = File.ReadAllLines file.FullName |> Array.toList |> List.map map in
      Fileset (opts, fs @ elements)
      // TODO filter comments, empty lines? |> Array.filter

    let changeBasedir dir (Fileset (opts,ps)) =
      Fileset ({opts with BaseDir = Some dir}, ps)
  // end of module Impl

  open Impl

  // lists the files
  let ls (filePattern:FilePattern) =
    let (Pattern pattern) = parse filePattern
    let startDir = Directory.GetCurrentDirectory()
    let files = listFiles [startDir] pattern
    files |> Seq.map (fun f -> FileInfo f) |> List.ofSeq |> FileList

  /// Gets the artifact file name
  let fullname (Artifact file) = file.FullName

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  /// Draft implementation of fileset execute
  let exec (Fileset (options,fileset)) =

    let startDir =
      match options.BaseDir with
      | None -> Directory.GetCurrentDirectory()
      | Some path -> path

    let files pattern =
      listFiles [startDir] pattern
      |> Seq.map (fun f -> FileInfo f) |> List.ofSeq

    fileset |> List.collect (function
      | Includes (Pattern pat) -> files pat
      | Excludes e -> [])  // TODO implement
    |> FileList

  let matches filePattern projectRoot =
    // IDEA: make relative path than match to pattern?
    // TODO implement. basedir matters! rules are global, so projectdir (current dir) is a basedir
    // matches "src/**/*.cs" "c:\!\src\a\b\c.cs" -> true

    let compareStr (s1:string) s2 =
      if s1 = null then s2 = null else s1.Equals(s2, System.StringComparison.OrdinalIgnoreCase)

    let comparePart p1 p2 =
      match p1,p2 with
      | Disk d1, Disk d2 -> compareStr d1 d2
      | Directory d1, Directory d2 -> compareStr d1 d2
      | DirectoryMask mask, Directory d2 -> let rx = fileMatchRegex mask  in rx.IsMatch(d2)
      | FileName f1, FileName f2 -> compareStr f1 f2
      | FileMask mask, FileName f2 -> let rx = fileMatchRegex mask in rx.IsMatch(f2)
      | _ -> false

    let rec comparePaths (mask:PatternPart list) (p:PatternPart list) =
      match mask,p with
      | [], [] -> true
      | [], x::xs -> false
      | m::ms, [] -> false
      | Recurse::ms, FileName d2::xs -> (comparePaths ms p)
      | Recurse::ms, Directory d2::xs -> (comparePaths mask xs) || (comparePaths ms p)
      | m::ms, x::xs -> (comparePart m x) && (comparePaths ms xs)

    let (Pattern mask) = Path.Combine(projectRoot,filePattern) |> parse

    let matchFile file =
      let (Pattern fileParts) = parse file
      comparePaths mask fileParts

    matchFile
      

  let (?==) p f = false

  /// Create a file set for specific file mask.
  let (!!) includes =
    Fileset (DefaultOptions, [includes |> parse |> Includes])

  /// Defines the basedir for a fileset
  let (<<<) (Fileset (opts,ps)) dir =
    Fileset ({opts with BaseDir = Some dir}, ps)

  /// Defines the empty fileset with a specified base dir
  let (~+) dir =
    Fileset ({DefaultOptions with BaseDir = Some dir}, [])

  type Fileset with
    static member (+) (fs1: Fileset, fs2: Fileset) = fs1 |> combineWith fs2
    static member (+) (fs1: Fileset, pat: FilePattern) = fs1 ++ pat
    static member (-) (fs1: Fileset, pat: FilePattern) = fs1 -- pat
    static member (@@) (fs1: Fileset, basedir: string) = fs1 <<< basedir

    /// Adds includes pattern to a fileset.
    static member (++) ((Fileset (opts,pts)), includes) =
      Fileset (opts, pts @ [includes |> parse |> Includes])

    /// Adds excludes pattern to a fileset.
    static member (--) (Fileset (opts,pts), excludes) =
      Fileset (opts, pts @ [excludes |> parse |> Excludes])
  end

  (******** builder ********)
  type FilesetBuilder() =

    let empty = Fileset (DefaultOptions, [])

    [<CustomOperation("basedir")>]
    member this.Basedir(fs,dir) = fs |> changeBasedir dir

    [<CustomOperation("includes")>]
    member this.Includes(fs:Fileset,pattern) = fs ++ pattern

    [<CustomOperation("join")>]
    member this.JoinFileset(fs1, fs2) = fs1 |> Impl.combineWith fs2

    [<CustomOperation("excludes")>]
    member this.Excludes(fs:Fileset, pattern) = fs -- pattern

    [<CustomOperation("includefile")>]
    member this.IncludeFile(fs, file) = fs |> combineWithFile (parse >> Includes) file

    [<CustomOperation("excludefile")>]
    member this.ExcludeFile(fs,file)  = fs |> combineWithFile (parse >> Excludes) file

    member this.Yield(())  = empty
    member this.Return(pattern:FilePattern) = empty ++ pattern

    member this.Combine(fs1, fs2) = fs1 |> Impl.combineWith fs2
    member this.Delay(f) = f()
    member this.Zero() = this.Yield ( () )

    member x.Bind(fs1:Fileset, f) = let fs2 = f() in fs1 |> Impl.combineWith fs2
    member x.For(fs, f) = x.Bind(fs, f)
    member x.Return(a) = x.Yield(a)

  let fileset = FilesetBuilder()

