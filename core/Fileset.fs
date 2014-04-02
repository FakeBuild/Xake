namespace Xake

[<AutoOpen>]
module Fileset =

  open System.IO

  type FilePattern = string

  /// Part of filesystem pattern
  type PatternPart =
    | FsRoot
    | Parent
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
  type FilesetType =
    | Fileset of FilesetOptions * FilesetElement list
    | FileList of FileInfo list

    // TODO better name for both options: FileNames, FilesetRules, FilesetPattern

  /// Default fileset options
  let DefaultOptions = {FilesetOptions.BaseDir = None; FailOnError = false}
  let Empty : FilesetType = FileList []

  /// Implementation module
  module internal Impl =

    open System.Text.RegularExpressions

    let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)
    let isMask (a:string) = a.IndexOfAny([|'*';'?'|]) >= 0
    let iif fn b c a = if fn a then b a else c a
    let fullname (f:DirectoryInfo) = f.FullName

    let joinPattern (Pattern p1) (Pattern p2) = Pattern (p1 @ p2)

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
    let parseDirFileMask (parseDir:bool) pattern =

      let mapPart = function
          | "**" -> Recurse
          | ".." -> Parent (* works well now with Path.Combine() *)
          | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> Disk(a)
          | a -> a |> iif isMask DirectoryMask Directory

      let dir = if parseDir then pattern else Path.GetDirectoryName(pattern)
      let parts = if dir = null then [||] else dir.Split([|'\\';'/'|], System.StringSplitOptions.RemoveEmptyEntries)

      // parse root "\" to FsRoot
      let fsroot = if dir <> null && (dir.StartsWith("\\") || dir.StartsWith("/")) then [FsRoot] else []
      let filepart = if parseDir then [] else [pattern |> Path.GetFileName |> (iif isMask FileMask FileName)]

      fsroot @ (Array.map mapPart parts |> List.ofArray) @ filepart
      |> Pattern
      
    /// Recursively applied the pattern rules to every item is start list
    let listFiles =
      let applyPart (paths:#seq<string>) = function
      | Disk d          -> seq {yield d + "\\"}
      | FsRoot          -> paths |> Seq.map Directory.GetDirectoryRoot
      | Parent          -> paths |> Seq.map (Directory.GetParent >> fullname)
      | Recurse         ->
            paths |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            |> Seq.append paths
      | DirectoryMask m -> paths |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, m, SearchOption.TopDirectoryOnly))
      | Directory d     -> paths |> Seq.map (fun dir -> Path.Combine(dir, d)) |> Seq.filter Directory.Exists
      | FileMask mask   -> paths |> Seq.collect (fun dir -> Directory.EnumerateFiles(dir, mask))
      | FileName f      -> paths |> Seq.map (fun dir -> Path.Combine(dir, f)) |> Seq.filter File.Exists
      in
      List.fold applyPart
    
    /// Parses file mask
    let parseFileMask = parseDirFileMask false

    /// Parses file mask
    let parseDir = parseDirFileMask true

    let eq s1 s2 = System.StringComparer.OrdinalIgnoreCase.Equals(s1, s2)

    let matchPart p1 p2 =
      match p1,p2 with
      | Disk d1, Disk d2 -> eq d1 d2
      | Directory d1, Directory d2 -> eq d1 d2
      | DirectoryMask mask, Directory d2 -> let rx = fileMatchRegex mask  in rx.IsMatch(d2)
      | FileName f1, FileName f2 -> eq f1 f2
      | FileMask mask, FileName f2 -> let rx = fileMatchRegex mask in rx.IsMatch(f2)
      | _ -> false

    let rec matchPathsImpl (mask:PatternPart list) (p:PatternPart list) =
      match mask,p with
      | [], [] -> true
      | [], x::xs -> false
      | m::ms, [] -> false

      (* parent support is not complete, supports up to two parent refs TODO normalize mask instead *)
      | Directory _::Parent::ms, _
      | Directory _::Directory _::Parent::Parent::ms, _
      | DirectoryMask _::Parent::ms, _
      | DirectoryMask _::DirectoryMask _::Parent::Parent::ms, _
        -> (matchPathsImpl ms p)

      | Directory _::Recurse::Parent::ms, _
        -> (matchPathsImpl (Recurse::ms) p)

      | Recurse::Parent::ms, _ -> (matchPathsImpl (Recurse::ms) p)  // ignore parent ref

      | Recurse::ms, FileName d2::xs -> (matchPathsImpl ms p)
      | Recurse::ms, Directory d2::xs -> (matchPathsImpl mask xs) || (matchPathsImpl ms p)
      | m::ms, x::xs -> (matchPart m x) && (matchPathsImpl ms xs)

    /// Returns true if a file name (parsedto p) matches specific file mask.      
    let matchesPattern (Pattern mask) file =
      let (Pattern fileParts) = parseFileMask file
      matchPathsImpl mask fileParts

    /// Draft implementation of fileset execute
    /// "Materializes fileset to a filelist
    let scan = function
      | FileList list as ff -> ff
      | Fileset (options,filesetItems) ->
        let startDir =
          match options.BaseDir with
          | None -> Directory.GetCurrentDirectory()   // TODO use project root
          | Some path -> path

        // TODO check performance, build function

        let includes (Pattern pat) src = listFiles [startDir] pat |> Seq.append src
        let excludes pat src =
          let matchFile = matchesPattern (joinPattern (startDir |> parseDir) pat) in
          src |> Seq.filter (matchFile >> not)

        let folditem i = function
          | Includes pat -> includes pat i
          | Excludes pat -> excludes pat i

        filesetItems |> Seq.ofList |> Seq.fold folditem Seq.empty<string> |> Seq.map (fun f -> FileInfo f) |> List.ofSeq |> FileList


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
    let rec combineWith f1 f2 =
      match f1,f2 with
      | (Fileset (o2, set2)),(Fileset (o1,set1)) -> Fileset(combineOptions o1 o2, set1 @ set2)
      | FileList list1, FileList list2 -> FileList (list1 @ list2)
      | FileList _, _ -> combineWith f1 (scan f2)
      | _, FileList _ -> combineWith (scan f1) f2

    /// More strict analog of combineWith, important to combine includes excludes
    let combineFilesetWith (Fileset (o2, set2)) (Fileset (o1,set1)) = Fileset(combineOptions o1 o2, set1 @ set2)

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
  let ls (filePattern:FilePattern) : FilesetType =
    Fileset (DefaultOptions, [filePattern |> parseFileMask |> Includes])

  /// Create a file set for specific file mask. The same as "ls"
  let (!!) = ls

  // TODO move Artifact stuff out of here
  /// Gets the artifact file name
  let fullname (file:Artifact) = file.FullName

  // gets an rule for file
  let ( ~& ) path :Artifact = (System.IO.FileInfo path)

  // changes file extension
  let (-<.>) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)

  /// Draft implementation of fileset execute
  /// "Materializes fileset to a filelist
  let scan = Impl.scan

  // let matches filePattern projectRoot
  let matches filePattern rootPath =
    // IDEA: make relative path than match to pattern?
    // matches "src/**/*.cs" "c:\!\src\a\b\c.cs" -> true

    // TODO alternative implementation, convert pattern to a match function using combinators
    Impl.matchesPattern <| joinPattern (rootPath |> parseDir) (filePattern |> parseFileMask)
      
  /// Gets the file list of specific fileset
  let rec getFiles = function
    | FileList list -> list
    | _ as fileset -> scan fileset |> getFiles

  /// Defines the empty fileset with a specified base dir
  let (~+) dir =
    Fileset ({DefaultOptions with BaseDir = Some dir}, [])

  type FilesetType with
    static member (+) (fs1: FilesetType, fs2: FilesetType) :FilesetType = fs1 |> combineWith fs2
    static member (+) (fs1: FilesetType, pat: FilePattern) = fs1 ++ pat
    static member (-) (fs1: FilesetType, pat: FilePattern) = fs1 -- pat
    static member (@@) (fs1: FilesetType, basedir: string) = fs1 |> Impl.changeBasedir basedir

    /// Conditional include/exclude operator
    static member (+?) (fs1: FilesetType, (condition:bool,pat: FilePattern)) = if condition then fs1 ++ pat else fs1
    static member (-?) (fs1: FilesetType, (condition:bool,pat: FilePattern)) = if condition then fs1 -- pat else fs1

    /// Adds includes pattern to a fileset.
    static member (++) ((Fileset (opts,pts)), includes) :FilesetType =
      Fileset (opts, pts @ [includes |> parseFileMask |> Includes])

    /// Adds excludes pattern to a fileset.
    static member (--) (Fileset (opts,pts), excludes) =
      Fileset (opts, pts @ [excludes |> parseFileMask |> Excludes])
  end

  (******** builder ********)
  type FilesetBuilder() =

    let empty = Fileset (DefaultOptions, [])

    [<CustomOperation("basedir")>]
    member this.Basedir(fs,dir) = fs |> changeBasedir dir

    [<CustomOperation("includes")>]
    member this.Includes(fs:FilesetType,pattern) = fs ++ pattern

    [<CustomOperation("includesif")>]
    member this.IncludesIf(fs:FilesetType,condition,pattern) =  fs +? (condition,pattern)

    [<CustomOperation("join")>]
    member this.JoinFileset(fs1, fs2) = fs1 |> Impl.combineWith fs2

    [<CustomOperation("excludes")>]
    member this.Excludes(fs:FilesetType, pattern) = fs -- pattern

    [<CustomOperation("excludesif")>]
    member this.ExcludesIf(fs:FilesetType, pattern) = fs -? pattern

    [<CustomOperation("includefile")>]
    member this.IncludeFile(fs, file) = fs |> combineWithFile (parseFileMask >> Includes) file

    [<CustomOperation("excludefile")>]
    member this.ExcludeFile(fs,file)  = fs |> combineWithFile (parseFileMask >> Excludes) file

    member this.Yield(())  = empty
    member this.Return(pattern:FilePattern) = empty ++ pattern

    member this.Combine(fs1, fs2) = fs1 |> Impl.combineFilesetWith fs2
    member this.Delay(f) = f()
    member this.Zero() = this.Yield ( () )

    member x.Bind(fs1:FilesetType, f) = let fs2 = f() in fs1 |> Impl.combineFilesetWith fs2
    member x.For(fs, f) = x.Bind(fs, f)
    member x.Return(a) = x.Yield(a)

  let fileset = FilesetBuilder()

