[<AutoOpen>]
module Xake.FileTasks

open Xake
open Common.impl

/// Removes the files
let rm (names : string list) =
  action {
    // TODO fail on error
    do! writeLog Level.Info "[rm] '%A'" names
    let! exitcode = _cmd "del /F /Q" names
    do! writeLog Level.Info "[rm] completed exitcode: %d" exitcode
  } 

/// Copies file
let cp (src: string) tgt =
  action {
    // TODO fail on error, multiplatform, normalize names, accept array
    do! need [src]
    do! writeLog Level.Info "[cp] '%A' -> '%s'" src tgt
    do! _cmd "copy " [src.Replace('/', '\\'); tgt] |> ActIgnore
  } 

