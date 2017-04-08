[<AutoOpen>]
module internal Xake.FileTasksImpl

open System.IO
open Xake

let ensureDirCreated fileName =
    let dir = fileName |> Path.GetDirectoryName

    if not <| System.String.IsNullOrEmpty(dir) then
        do dir |> Directory.CreateDirectory |> ignore