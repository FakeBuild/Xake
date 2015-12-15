namespace Xake

open System.IO
open System.Text.RegularExpressions

module Path =

    type Part =
        | FsRoot
        | Parent
        | CurrentDir
        | Disk of string
        | DirectoryMask of string
        | Directory of string
        | Recurse
        | FileMask of string
        | FileName of string
        // IDEA | Named of string * PatternPart

    type PathMask = PathMask of Part list

    //type Path = Path of Part list
    (*
      path could be:
      c:\Windows\             - dir path
      c:\Windows\paint.exe    - absolute file path
      \Windows\paint.exe      - relative file path
      \Windows\               - relative dir path

      \*\paint.exe            - relative file mask
      c:\*\paint.exe          - absolute file mask

      ops:
      join: abs + relative      -> abs
      join: relative + relative -> relative
      match path mask

      invariants:
      * paths/masks are normalized
    *)
//    type T =
//        | Path of Part list
//        | PathMask of Part list
//        | AbsPath of Part list

    module private impl =
        let dirSeparator = Path.DirectorySeparatorChar
        let notNullOrEmpty = System.String.IsNullOrEmpty >> not

        let driveRegex = Regex(@"^[A-Za-z]:$", RegexOptions.Compiled)
        let isMask (a:string) = a.IndexOfAny([|'*';'?'|]) >= 0
        let iif fn b c a = match fn a with | true -> b a | _ -> c a
        
        let isRoot = function | FsRoot::_ | Disk _::_ -> true | _ -> false

        /// <summary>
        /// Normalizes the pattern by resolving parent references and removing \.\
        /// </summary>
        /// <param name="pattern"></param>
        let rec normalize = function
            | [] -> []
            | x::[] -> [x]
            | x::tail ->               
                match x::(normalize tail) with
                | Directory _::Parent::t -> t
                | CurrentDir::t -> t
                | _ as rest -> rest

        /// <summary>
        /// Maps part of file path to a path part.
        /// </summary>
        /// <param name="mapPart"></param>
        let mapPart isLast = function
            | "**" -> Recurse
            | "." -> CurrentDir
            | ".." -> Parent (* works well now with Path.Combine() *)
            | a when a.EndsWith(":") && driveRegex.IsMatch(a) -> Disk(a)
            | a when isLast = false -> a |> iif isMask DirectoryMask Directory
            | a -> a |> iif isMask FileMask FileName

        let parse isLastPart pattern =
         
            if notNullOrEmpty pattern then
                let parts = pattern.Split([|'\\'; '/'|], System.StringSplitOptions.RemoveEmptyEntries)
                let fsroot = pattern.[0] |> function | '\\' | '/' -> [FsRoot] | _ -> []
                in
                let isLast = isLastPart parts
                fsroot @ (parts |> Array.mapi (isLast >> mapPart) |> List.ofArray)
                    |> normalize |> PathMask
            else
                PathMask []

        /// <summary>
        /// supplementary function for parsing directory
        /// </summary>
        let isLastPartForDir _ _ = false
        /// <summary>
        /// supplementary function for parsing file
        /// </summary>
        let isLastPartForFile (parts:_ array) = (=) (parts.Length-1)

    module private PicklerImpl =

        open Pickler

        let patternpart=
            alt(function
                | FsRoot -> 0
                | Parent -> 1
                | Disk _ -> 2
                | DirectoryMask _ -> 3
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

        let pattern = wrap(PathMask, fun(PathMask pp) -> pp) (list patternpart)

    module internal matchImpl =

        let eq s1 s2 = System.StringComparer.OrdinalIgnoreCase.Equals(s1, s2)

        let fileMatchRegex (pattern:string) =
            let c2r = function
                | '*' -> ".*"
                | '.' -> "[.]"
                | '?' -> "."
                | '$' -> "\$"
                | '^' -> "\^"
                | ch -> System.String(ch,1)
            let pat = (pattern.ToCharArray() |> Array.map c2r |> System.String.Concat)
            Regex(@"^" + pat + "$", RegexOptions.Compiled + RegexOptions.IgnoreCase)    // TODO ignore case is optional (system-dependent)

        let matchPart p1 p2 =
            match p1,p2 with
            | Disk d1, Disk d2 -> eq d1 d2
            | Directory d1, Directory d2 -> eq d1 d2
            | DirectoryMask mask, Directory d2 -> let rx = fileMatchRegex mask    in rx.IsMatch(d2)
            | FileName f1, FileName f2 -> eq f1 f2
            | FileMask mask, FileName f2 -> let rx = fileMatchRegex mask in rx.IsMatch(f2)
            | FsRoot, FsRoot -> true
            | _ -> false

        let rec matchPaths (mask:Part list) (p:Part list) =
            match mask,p with
            | [], [] -> true
            | [], _ | _, [] -> false

            | Directory _::Recurse::Parent::ms, _
                -> (matchPaths (Recurse::ms) p)

            | Recurse::Parent::ms, _ -> (matchPaths (Recurse::ms) p)    // ignore parent ref

            | Recurse::ms, (FileName _)::_ -> (matchPaths ms p)
            | Recurse::ms, Directory _::xs -> (matchPaths mask xs) || (matchPaths ms p)
            | m::ms, x::xs -> (matchPart m x) && (matchPaths ms xs)

    // API
    let pickler = PicklerImpl.pattern

    let toFileSystem (PathMask pp) = "" // TODO implement

    /// <summary>
    /// Joins two patterns.
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    let join (PathMask p1) (PathMask p2) =
        if impl.isRoot p2 then PathMask p2
        else p1 @ p2 |> impl.normalize |> PathMask

    /// <summary>
    /// Converts Ant-style file pattern to a list of parts. Assumes the path specified
    /// </summary>
    let parseDir = impl.parse impl.isLastPartForDir

    /// <summary>
    /// Converts Ant-style file pattern to a list of parts.
    /// </summary>
    let parse = impl.parse impl.isLastPartForFile

    /// <summary>
    /// Returns true if a file name (parsed to p) matches specific file mask.         
    /// </summary>
    /// <param name="mask"></param>
    /// <param name="file"></param>
    let matchesPattern (PathMask mask) file =

        // TODO alternative implementation, convert pattern to a match function using combinators
        // TODO refine signature
        let (PathMask fileParts) = file |> impl.parse impl.isLastPartForFile in
        matchImpl.matchPaths mask fileParts

    // let matches filePattern projectRoot
    let matches filePattern rootPath =
        // IDEA: make relative path than match to pattern?
        // matches "src/**/*.cs" "c:\!\src\a\b\c.cs" -> true

        matchesPattern <| join (parseDir rootPath) (parse filePattern)
