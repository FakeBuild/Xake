#r "../bin/Xake.dll"

open Xake
open Xake.Tasks
open System.IO

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