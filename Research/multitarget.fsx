#r "../bin/Xake.dll"

open Xake
open Xake.Tasks
open System.IO
open System.Text.RegularExpressions

let masks = ["a/**/file1.exe"; "a/**/file2.xml"]
let maskPaths = masks |> List.map Path.parse

// getting a common part
let rec extractCommonPath path (paths: Path.PathMask list) =

    let firstParts, tailParts =
        paths |> List.map (function | Path.PathMask (x::tail) -> Some x, Path.PathMask tail | x -> None, x) |> List.unzip
    
    firstParts
    |> function
        |f::tail when tail |> List.forall ((=) f) -> f
        | _ -> None
    |> function
        | Some commonPart -> (path @ [commonPart], tailParts) ||> extractCommonPath
        | _ -> (path, paths)

/// Gets true if file name does not contain any wildcards
let isRelativePath (Path.PathMask parts) =
    parts |> List.forall(function
        |Path.Directory _ -> true
        |Path.FileName _ -> true
        |Path.Parent -> true
        |_ -> false
        )

let processComplexPat pathArray =
    let common, masks = pathArray |> List.map Path.parse |> extractCommonPath []

    // TODO non empty
    match masks |> List.tryFind (isRelativePath >> not) with
    |Some broken -> printfn "ERROR: %A is not relative" broken
    | _ ->
        do printfn "Common part is %A" common
        do printfn "SUCCESS: %A" masks


let result = maskPaths |> extractCommonPath []
let r1 = ["a/**/a/file1.exe"; "a/**/file2.xml"] |> List.map Path.parse |> extractCommonPath []
let r2 = [""; "a/**/file2.xml"] |> List.map Path.parse |> extractCommonPath []
let r3 = ["a/**/file2.xml"] |> List.map Path.parse |> extractCommonPath []

do ["a/**/a/file1.exe"; "a/**/file2.xml"] |> processComplexPat
do ["a/**/a/file1.exe"; "a/**/../file2.xml"] |> processComplexPat
do ["a/**/a/file1.exe"; "a/**"] |> processComplexPat

let (basePart,pat) = "c:/a/**", "al/file1.exe"
let target = "c:/a/b/c/d/al/file1.exe"
let groups = target |> Path.matches (basePart </> pat) ""

let extractBasePath targetName pattern =

    let (Path.PathMask targetParts) = Path.parse targetName
    let (Path.PathMask patternParts) = Path.parse pattern

    let rec stripItem = function
        | a::tail1, b::tail2 when a = b -> (tail1, tail2) |> stripItem
        | x -> x

    (List.rev targetParts, List.rev patternParts)
    |> stripItem
    |> fun x -> printf "%A" x; x
    |> function | baseRev, [] -> baseRev |> (List.rev >> Path.PathMask >> Path.toFileSystem >>  Some) | _ -> None

let xx = extractBasePath target pat
// idea1: parse target name, strip pat from end
// problem: how to know the base?
let xx1 = Path.parse ("../file1.exe"), Path.parse "c:/a/b/c/d/al/file1.exe"

// solution: only allow [Directory...]/FileName in pat



let patterns = ["a/**/bin/*.exe"; "a/**/bin/*.xml"; "a/**/*.txt"]
// after rule is matched take all wildcards and apply to other patterns
// - new "match" implementation with wildcards detection
// - requires all wildcards to match
// - need duplicating named match groups

let x = processComplexPat patterns
let common, masks = patterns |> List.map Path.parse |> extractCommonPath []

let wilcard2regexMap =
   ["**", "(.*)"
    "*", "([^/\\\\]*)"
    "?", "([^/\\\\])"
    ".", "\\."
    "$", "\\$"
    "^", "\\^"
   ] |> dict

let wildcardToRegex (m:Match) =
    match m.Groups.Item("tag") with
    | t when not t.Success ->
        match wilcard2regexMap.TryGetValue(m.Value) with
        | true, v -> v
        | _ -> m.Value
    | t -> "(?<" + t.Value + ">"

let maskToRegex (pattern:string) =

    let pat = Regex.Replace(pattern, @"\((?'tag'\w+?)\:|\*\*|([*.?$^])", wildcardToRegex)
    in
    Regex(@"^" + pat + "$", RegexOptions.Compiled + RegexOptions.IgnoreCase)    // TODO ignore case is optional (system-dependent)

System.IO.File.Exists("\\\\d")
let xx = maskToRegex "\\\\a/bin/*.exe"
do dump <| xx.Match("a/bin/pc.exe")

// generate for "a/bin/*.txt" -> "a/bin/$1\.txt"
// generate for "a/(TAG:bin)/*.txt" -> "a/${TAG}/$1\.txt"

let wildcardToPat () =
    let mutable i = 0    
    let fn (m:Match) =
        match m.Groups.Item("tag") with
        | t when t.Success -> sprintf "${%s}" t.Value
        | t ->
            i <- i + 1
            sprintf "$%i" i
    fn

let matchWildcardsRegex = Regex(@"\((?'tag'\w+?)\:[^)]+\)|\*\*|\*|\?")
let px: Match -> string = wildcardToPat ()

let pat1 = "a?/(TAG:*bin)/*.txt"
let pat2 = "a?/(TAG:*bin)/*.dll"
let px2: Match -> string = wildcardToPat ()
let pat = Regex.Replace(pat1, matchWildcardsRegex, (wildcardToPat()))
let mask2 = Regex.Replace(pat2, matchWildcardsRegex, px2)
let name2 = Regex.Replace(pat1, matchWildcardsRegex, mask2)

do dump <| (maskToRegex "c:/(f:**)/(fname:*.exe)").Match("c:/b/c/asd.exe")
do dump <| (maskToRegex "c:/**/(fname:*.exe)").Match("c:/asd.exe")

(maskToRegex "c:/(f:**)/(fname:*.exe)").GetGroupNames() |> Array.filter(fun s -> s.[0] |> System.Char.IsDigit |> not)

let getWildcards pattern =
    Path.matchGroups ((<>) "0") pattern "" >> Option.map Map.ofList

let results = getWildcards "a?/(TAG:*.*)/*.txt/**/+" "a1/win32-bin.aa/omega.txt/1/2/3/4/+"
let results = getWildcards "a?/(TAG:*.*)/*.txt" "a1/win32-bin.aa/omega.txt"

let wildcardsRegex = Regex(@"\*\*|\*|\?", RegexOptions.Compiled)
let patternTagRegex = Regex(@"\((?'tag'\w+?)\:[^)]+\)", RegexOptions.Compiled)
let applyWildcards (maybeMatches: Map<string,string> option) =
    let replace (regex:Regex) (evaluator: Match -> string) text = regex.Replace(text, evaluator)
    match maybeMatches with
    | None -> id
    | Some matches ->
        fun pat ->
            let mutable i = 0
            let ifNone x = function |Some x -> x | _ -> x
            let evaluator m =
                i <- i + 1
                matches |> Map.tryFind (i.ToString()) |> ifNone ""
            let evaluatorTag (m: Match) =
                let tagValue = m.Groups.["tag"].Value
                matches |> Map.tryFind tagValue |> ifNone ""
            pat
            |> replace wildcardsRegex evaluator
            |> replace patternTagRegex evaluatorTag

let a = applyWildcards results
let newStr = a "b?/(TAG:*--*)/*.exe"
let newStr1 = a "b?/(TAG:*--*)/*.exe"

let c = applyWildcards results "b?/(TAG:_)/*.exe"
let d = applyWildcards results "b?/*-*/*.exe"

let applyWildcards (matchDict: Map<string,string>) =
    let mutable i = 0
    let fn (m:Match) =
        match m.Groups.Item("tag") with
        | t when t.Success -> matchDict.[t.Value]
        | t ->
            i <- i + 1
            matchDict.[i.ToString()]
    fn


//
// wildcards with tags https://regex101.com/r/FA721x/1

let matchWildcardsRegex = Regex(@"(\*\*)|(\*)|(\?)|\((?'tag'\w+?)\:(?:(\*\*)|(\*)|(\?)|[^)])+\)")
// let matchWildcardsRegex = Regex(@"(\*\*)|(\*)|(\?)|\((\w+?)\:(?:(\*\*|\*|\?)|[^\)])+\)")


let mm = matchWildcardsRegex.Matches("**/a/(TAG:x?.*bin/**)/*.dll")
for m in mm
  do
    do printfn "==%A" m
    do dump m

pat2
let name2 =
    let mutable i = 0
    matchWildcardsRegex.Replace(
        "a?/(TAG:*bin)/r.dll",
        (fun m ->
            printfn "match %s" (m.ToString())
            match m.Groups.Item("tag") with
            | t when t.Success -> "tag:" + t.Value
            | t ->
                i <- i + 1
                "i:" + (i.ToString())
            |> printfn "%s"
            ""
        )
    )

let name3 = Regex.Replace("a?/(TAG:*bin)/*.dll", matchWildcardsRegex, applyWildcards resPat1Dict)

let dump (m: Match) =
    if m.Success then
        printfn "%i groups found'" m.Groups.Count
        for group in m.Groups do
            printfn "Group %s: '%A'" group.Name group.Value
        printfn "%i captures" m.Captures.Count
        for c in m.Captures do
            printfn "Capture '%A'" c


// TODO match all groups, wildcards and "**" (recurse)
// TODO apply matches to all other name parts

// idea2: special matches implementation

// idea3:
(*
 // match mask by mask: every mask has corresponding mask in every pattern
 // easy to match every part
 generate "x/y/bin/aaa.dll" ["**/bin/aaa.dll"; "**/aaa.xml"]

 // idea: grab all files
 "**/tracks/**/*.mp3"
 generate "x/y/bin/aaa.dll" ["**/bin/aaa.dll"; "**/aaa.xml"]

 // generate by applying groups to mask
 ["(xx:**)/bin/aaa.dll"; "(xx:**)/bin/aaa.xml"]

*)


open Xake.Path
let rec matchPaths (baseMask:Path.Part list) (mask:Path.Part list) (p:Path.Part list) groups =
    match mask,p with
    | [], [] -> Some groups
    | [], _ | _, [] -> None

    | Recurse::ms, (FileName _)::_ -> matchPaths ms p groups
    | Recurse::ms, Directory _::xs ->
        match matchPaths mask xs groups with
        | None -> matchPaths ms p groups
        | mm -> mm
    | m::ms, x::xs ->
        Path.matchPart m x |> Option.bind (fun gg -> matchPaths ms xs (groups @ gg))


let getTargetFiles () = recipe {
    return [File.make "a"; File.make "b"]
}

do xakeScript {
    rules [
        "main" => recipe {
            let! opt = getCtxOptions()
            do! need ["a/file1"; "a/file2"]
            do! rm {dir "a"}
            // let dd = Path.parseDir "*"|> Fileset.listByMask opt.ProjectRoot
            // do! trace Level.Command "dd: %A" dd
        }

        // simple multitarget rule
        ("a", ["file1"; "file2"]) ..>> recipe {
            let! [target1; target2] = getTargetFiles()
            do! writeText "hello world"
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
        }

        // group rule
        // challenge: how to identify common part for both names
        ("a/**", ["file1.exe"; "file2.xml"]) ..>> recipe {
            let! [target1; target2] = getTargetFiles()
            do! writeText "hello world"
            do File.WriteAllText(target1.FullName, "file1")
            do File.WriteAllText(target2.FullName, "file2")
        }
    ]
}