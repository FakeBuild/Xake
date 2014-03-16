#load "Types.fs"
#load "Fileset.fs"

open Xake.DomainTypes
open Xake.Fileset

// Define your library scripting code here
let files = ls "..\*.*"

let names fileset =
 let (Files ff) = fileset
 ff |> List.map (fun (Artifact (f,_)) -> f.Name) |> List.toArray

