#r @"..\bin\Xake.Core.dll"
open Xake

// Define your library scripting code here
let files = ls "..\*.*"

let curdir = System.IO.Directory.GetCurrentDirectory()
let dump = toFileList curdir >> List.map (fun f -> f.Name) >> List.iter (printf "%s\r\n") >> ignore

do dump (ls "*.*" -- "*.tmp")