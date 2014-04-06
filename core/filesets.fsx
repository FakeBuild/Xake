#r @"..\bin\Xake.Core.dll"
open Xake

// Define your library scripting code here
let files = ls "..\*.*"

let dump = getFiles >> List.map (fun f -> f.Name) >> List.iter (printf "%s\r\n") >> ignore

do dump (ls "*.*" -- "*.tmp")