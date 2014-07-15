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

    type FilesetOptions = {FailOnEmpty:bool; BaseDir:string option}

    // Fileset is either set of rules or list of files (materialized)
    type Fileset = Fileset of FilesetOptions * FilesetElement list
    type Filelist = Filelist of FileInfo list

    /// Default fileset options
    let DefaultOptions = {FilesetOptions.BaseDir = None; FailOnEmpty = false}
    
    let Empty = Fileset (DefaultOptions,[])
    let EmptyList = Filelist []

    /// Implementation module
    module private Impl =

        open System.Text.RegularExpressions

        let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)
        let isMask (a:string) = a.IndexOfAny([|'*';'?'|]) >= 0
        let iif fn b c a = match fn a with | true -> b a | _ -> c a
        let fullname (f:DirectoryInfo) = f.FullName

        let joinPattern (Pattern p1) (Pattern p2) = Pattern (p1 @ p2)

        /// Builds the regexp for testing file part
        let fileMatchRegex (pattern:string) =
            let c2r = function
                | '*' -> ".*"
                | '.' -> "[.]"
                | '?' -> "."
                | ch -> System.String(ch,1)
            let pat = (pattern.ToCharArray() |> Array.map c2r |> System.String.Concat)
            Regex(@"^" + pat + "$", RegexOptions.Compiled + RegexOptions.IgnoreCase)    // TODO ignore case is optional (system-dependent)

        /// Converts Ant-style file pattern to a list of parts
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

            Pattern <| fsroot @ (Array.map mapPart parts |> List.ofArray) @ filepart
            
        /// Recursively applies the pattern rules to every item is start list
        let listFiles startIn (Pattern pat) =

            let scanall dir = Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)

            // The pattern without mask become "explicit" file reference which is always included in resulting file list, regardless file presence. See impl notes for details.
            let isExplicitRule = pat |> List.exists (function | DirectoryMask _ | FileMask _ -> true | _ -> false)
            let filterDir = if isExplicitRule then Seq.filter Directory.Exists else id
            let filterFile = if isExplicitRule then Seq.filter File.Exists else id

            let applyPart (paths:#seq<string>) = function
            | Disk d          -> seq {yield d + "\\"}
            | FsRoot          -> paths |> Seq.map Directory.GetDirectoryRoot
            | Parent          -> paths |> Seq.map (Directory.GetParent >> fullname)
            | Recurse         -> paths |> Seq.collect scanall |> Seq.append paths
            | DirectoryMask m -> paths |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, m, SearchOption.TopDirectoryOnly))
            | Directory d     -> paths |> Seq.map (fun dir -> Path.Combine(dir, d)) |> filterDir
            | FileMask mask   -> paths |> Seq.collect (fun dir -> Directory.EnumerateFiles(dir, mask))
            | FileName f      -> paths |> Seq.map (fun dir -> Path.Combine(dir, f)) |> filterFile
            in
            pat |> List.fold applyPart startIn
        
        /// Parses file mask
        let parseFileMask = parseDirFileMask false

        /// Parses file mask
        let parseDir = parseDirFileMask true

        let eq s1 s2 = System.StringComparer.OrdinalIgnoreCase.Equals(s1, s2)

        let matchPart p1 p2 =
            match p1,p2 with
            | Disk d1, Disk d2 -> eq d1 d2
            | Directory d1, Directory d2 -> eq d1 d2
            | DirectoryMask mask, Directory d2 -> let rx = fileMatchRegex mask    in rx.IsMatch(d2)
            | FileName f1, FileName f2 -> eq f1 f2
            | FileMask mask, FileName f2 -> let rx = fileMatchRegex mask in rx.IsMatch(f2)
            | FsRoot, FsRoot -> true
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

            | Recurse::Parent::ms, _ -> (matchPathsImpl (Recurse::ms) p)    // ignore parent ref

            | Recurse::ms, FileName d2::xs -> (matchPathsImpl ms p)
            | Recurse::ms, Directory d2::xs -> (matchPathsImpl mask xs) || (matchPathsImpl ms p)
            | m::ms, x::xs -> (matchPart m x) && (matchPathsImpl ms xs)

        /// Returns true if a file name (parsedto p) matches specific file mask.            
        let matchesPattern (Pattern mask) file =
            let (Pattern fileParts) = parseFileMask file in
            matchPathsImpl mask fileParts

        let private ifNone v2 = function | None -> v2 | Some v -> v

        /// Draft implementation of fileset execute
        /// "Materializes" fileset to a filelist
        let scan root (Fileset (options,filesetItems)) =

            let startDir = options.BaseDir |> ifNone root
            // TODO check performance, build function
            let includes src = [startDir] |> listFiles >> Seq.append src
            let excludes src pat =
                let matchFile = pat |> joinPattern (startDir |> parseDir) |> matchesPattern in
                src |> Seq.filter (matchFile >> not)

            let folditem i = function
                | Includes pat -> includes i pat
                | Excludes pat -> excludes i pat

            filesetItems |> Seq.ofList |> Seq.fold folditem Seq.empty<string> |> Seq.map (fun f -> FileInfo f) |> List.ofSeq |> Filelist

        // combines two fileset options
        let combineOptions (o1:FilesetOptions) (o2:FilesetOptions) =
            {DefaultOptions with
                BaseDir =
                    match o1.BaseDir,o2.BaseDir with
                    | Some _, Some _ -> failwith "Cannot combine filesets with basedirs defined in both (not implemented)"
                    | Some _, None -> o1.BaseDir
                    | _ -> o2.BaseDir
                FailOnEmpty = o1.FailOnEmpty || o2.FailOnEmpty}

        // combines two filesets
        let combineWith (Fileset (o2, set2)) (Fileset (o1,set1)) = Fileset(combineOptions o1 o2, set1 @ set2)

        // Combines result of reading file to a fileset
        let combineWithFile map (file:FileInfo) (Fileset (opts,fs)) =
            let elements = File.ReadAllLines file.FullName |> Array.toList |> List.map map in
            Fileset (opts, fs @ elements)
            // TODO filter comments, empty lines? |> Array.filter

        let changeBasedir dir (Fileset (opts,ps)) =   Fileset ({opts with BaseDir = Some dir}, ps)
        let changeFailonEmpty f (Fileset (opts,ps)) = Fileset ({opts with FailOnEmpty = f}, ps)

    // end of module Impl

    /// Fileset persistance implementation
    module private PicklerImpl =

        open Pickler

        let filesetoptions =
            wrap(
                (fun(foe,bdir) -> {FilesetOptions.FailOnEmpty = foe; BaseDir = bdir}),
                fun o -> (o.FailOnEmpty, o.BaseDir))
                (pair bool (option str))

        let patternpart=
            alt(function
                | FsRoot -> 0
                | Parent -> 1
                | Disk _ -> 2
                | DirectoryMask m -> 3
                | Directory _ -> 4
                | Recurse -> 5
                | FileMask _ -> 6
                | FileName _ -> 7)
              [|
                wrap0 FsRoot
                wrap0 Parent
                wrap (Disk, fun (Disk d) -> d) str
                wrap (DirectoryMask, fun (DirectoryMask d) -> d) str
                wrap (Directory, fun (Directory d) -> d) str
                wrap0 Recurse
                wrap (FileMask, fun (FileMask m) -> m) str
                wrap (FileName, fun (FileName m) -> m) str
              |]

        let pattern = wrap(Pattern, fun(Pattern pp) -> pp) (list patternpart)

        let filesetElement =
          alt
            (function | Includes _ -> 0 | Excludes _ -> 1)
            [|
              wrap (Includes, fun (Includes p) -> p) pattern
              wrap (Excludes, fun (Excludes p) -> p) pattern
            |]

        let fileinfo = wrap((fun n -> System.IO.FileInfo n), fun fi -> fi.FullName) str

        let fileset  = wrap(Fileset, fun (Fileset (o,l)) -> o,l) (pair filesetoptions (list filesetElement))
        let filelist = wrap(Filelist, fun (Filelist l) -> l) (list fileinfo)

    open Impl

    /// Gets the pickler for fileset type
    let filesetPickler = PicklerImpl.fileset
    let filelistPickler = PicklerImpl.filelist

    // creates a new fileset with default options
    let ls (filePattern:FilePattern) =
        Fileset (DefaultOptions, [filePattern |> parseFileMask |> Includes])

    /// Create a file set for specific file mask. The same as "ls"
    let (!!) = ls

    // TODO move Artifact stuff out of here
    /// Gets the artifact file name
    let getFullname = function
        | FileTarget file -> file.FullName
        | PhonyAction name -> name

    // Gets the short artifact name
    let getShortname = function
        | FileTarget file -> file.Name
        | PhonyAction name -> name

    // Gets whether artifact exists
    let exists = function
        | FileTarget file -> file.Exists
        | PhonyAction _ ->    false // TODO this is suspicious

    // changes file extension
    let (-.) (file:FileInfo) newExt = Path.ChangeExtension(file.FullName,newExt)
    let (</>) path1 path2 = Path.Combine(path1, path2)
    let (<.>) path ext = if System.String.IsNullOrWhiteSpace(ext) then path else path + "." + ext

    let parseFileMask = Impl.parseDirFileMask false
    let parseDirMask = Impl.parseDirFileMask true

    // let matches filePattern projectRoot
    let matches filePattern rootPath =
        // IDEA: make relative path than match to pattern?
        // matches "src/**/*.cs" "c:\!\src\a\b\c.cs" -> true

        // TODO alternative implementation, convert pattern to a match function using combinators
        Impl.matchesPattern <| joinPattern (rootPath |> parseDir) (filePattern |> parseFileMask)
            
    /// "Materializes fileset to a filelist
    let toFileList = Impl.scan

    type ListDiffType<'a> = | Added of 'a | Removed of 'a

    /// Compares two file lists and returnd differences list
    let compareFileList (Filelist list1) (Filelist list2) =

        let fname (f:System.IO.FileInfo) = f.FullName
        let setOfNames = List.map fname >> Set.ofList
        
        let set1, set2 = setOfNames list1, setOfNames list2

        let removed = Set.difference set1 set2 |> List.ofSeq |> List.map (Removed)
        let added = Set.difference set2 set1 |> List.ofSeq |> List.map (Added)

        removed @ added

    /// Defines the empty fileset with a specified base dir
    let (~+) dir =
        Fileset ({DefaultOptions with BaseDir = Some dir}, [])

    // defines various operations
    type Fileset with
        static member (+) (fs1, fs2: Fileset) :Fileset = fs1 |> combineWith fs2
        static member (+) (fs1: Fileset, pat) = fs1 ++ pat
        static member (-) (fs1: Fileset, pat) = fs1 -- pat
        static member (@@) (fs1, basedir) = fs1 |> Impl.changeBasedir basedir
        static member (@@) (Fileset (_,lst), options) = Fileset (options,lst)

        /// Conditional include/exclude operator
        static member (+?) (fs1: Fileset, (condition:bool,pat: FilePattern)) = if condition then fs1 ++ pat else fs1
        static member (+?) (fs1: Fileset, (condition:bool,fs2: Fileset)) :Fileset = if condition then fs1 |> combineWith fs2 else fs1
        static member (-?) (fs1: Fileset, (condition:bool,pat: FilePattern)) = if condition then fs1 -- pat else fs1

        /// Adds includes pattern to a fileset.
        static member (++) ((Fileset (opts,pts)), includes) :Fileset =
            Fileset (opts, pts @ [includes |> parseFileMask |> Includes])

        /// Adds excludes pattern to a fileset.
        static member (--) (Fileset (opts,pts), excludes) =
            Fileset (opts, pts @ [excludes |> parseFileMask |> Excludes])
    end

    (******** builder ********)
    type FilesetBuilder() =

        [<CustomOperation("failonempty")>]
        member this.FailOnEmpty(fs,f) = fs |> changeFailonEmpty f

        [<CustomOperation("basedir")>]
        member this.Basedir(fs,dir) = fs |> changeBasedir dir

        [<CustomOperation("includes")>]
        member this.Includes(fs:Fileset,pattern) = fs ++ pattern

        [<CustomOperation("includesif")>]
        member this.IncludesIf(fs:Fileset,condition, pattern:FilePattern) =    fs +? (condition,pattern)

        [<CustomOperation("join")>]
        member this.JoinFileset(fs1, fs2) = fs1 |> Impl.combineWith fs2

        [<CustomOperation("excludes")>]
        member this.Excludes(fs:Fileset, pattern) = fs -- pattern

        [<CustomOperation("excludesif")>]
        member this.ExcludesIf(fs:Fileset, pattern) = fs -? pattern

        [<CustomOperation("includefile")>]
        member this.IncludeFile(fs, file) = fs |> combineWithFile (parseFileMask >> Includes) file

        [<CustomOperation("excludefile")>]
        member this.ExcludeFile(fs,file)    = fs |> combineWithFile (parseFileMask >> Excludes) file

        member this.Yield(())    = Empty
        member this.Return(pattern:FilePattern) = Empty ++ pattern

        member this.Combine(fs1, fs2) = fs1 |> Impl.combineWith fs2
        member this.Delay(f) = f()
        member this.Zero() = this.Yield ( () )

        member x.Bind(fs1:Fileset, f) = let fs2 = f() in fs1 |> Impl.combineWith fs2
        member x.For(fs, f) = x.Bind(fs, f)
        member x.Return(a) = x.Yield(a)

    let fileset = FilesetBuilder()

