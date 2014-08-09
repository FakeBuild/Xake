[<AutoOpen>]
module Xake.FileTasks

open System.IO
open Xake
open Common.impl

/// Removes the files
let rm (names : string list) =

  let deleteByMask root mask =
    let (Filelist files) = Fileset.ls mask |> (toFileList root)
    files |> List.map (fun f -> f.FullName) |> List.iter File.Delete
    
  action {
    do! writeLog Level.Info "[rm] '%A'" names
    let! options = getCtxOptions()

    names |> List.iter (deleteByMask options.ProjectRoot)
    do! writeLog Level.Info "[rm] Completed"
  } 

/// Copies file
let cp (src: string) tgt =
  action {
    // TODO fail on error, multiplatform, normalize names, accept array
    do! need [src]
    do! writeLog Level.Info "[cp] '%A' -> '%s'" src tgt

    File.Copy(src, tgt, true)
  } 

