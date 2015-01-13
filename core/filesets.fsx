#r @"..\bin\Xake.Core.dll"
open Xake

// Define your library scripting code here
let files = ls "..\*.*"

let curdir = System.IO.Directory.GetCurrentDirectory()
let exList (Filelist l) = l
let dump = toFileList curdir >> exList >> List.map (fun f -> f.Name) >> List.iter (printf "%s\r\n") >> ignore

do dump (ls "*.*" -- "*.tmp")

let f = DotNetFwk.locateFramework <| Some "2.0"
let m = DotNetFwk.locateFramework <| Some "mono-3.5"
let h = DotNetFwk.locateFramework <| None