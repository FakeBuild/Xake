#load "Types.fs"
#load "Fileset.fs"

open Xake

// Define your library scripting code here
let files = ls "..\*.*"

let names fileset =
 let (Files ff) = fileset
 ff |> List.map (fun (FileArtifact (f,_)) -> f.Name) |> List.toArray

// System.IO.Path.
let file = "build/hello.c"
let mask = "*.c"

let test (mask: string) file =
  let c = function
    | '*' -> ".+"
    | '.' -> "[.]"
    | '?' -> "."
    | ch -> System.String(ch,1)
  let pattern = "^" + System.String.Concat(mask.ToCharArray() |> Array.map c) + "$"
  if System.Text.RegularExpressions.Regex.Matches(file, pattern).Count > 0 then true else false
