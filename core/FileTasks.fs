[<AutoOpen>]
module Xake.FileTasks

open Xake

/// Removes the files
let rm (names : string list) =
  action {
    // TODO fail on error
    do log Level.Info "[rm] '%A'" names
    let! exitcode = cmd "del /F /Q" names
    do log Level.Info "[rm] completed exitcode: %d" exitcode
  } 

