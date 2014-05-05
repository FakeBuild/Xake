[<AutoOpen>]
module Xake.FileTasks

open Xake

/// Removes the files
let rm (names : string list) =
  action {
    // TODO fail on error
    do! writeLog Level.Info "[rm] '%A'" names
    let! exitcode = _cmd "del /F /Q" names
    do! writeLog Level.Info "[rm] completed exitcode: %d" exitcode
  } 

