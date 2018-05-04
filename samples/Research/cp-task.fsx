// #r "../bin/Xake.Core.dll"
#r "../core/bin/Debug/net46/Xake.dll"

open Xake
open System.IO

let f = fileset {
    basedir "samples"
    includes "a/**/"
}
let projectRoot = "."

let (Filelist files) = f |> Fileset.toFileList projectRoot
let (Fileset ({BaseDir = Some basedir}, _)) = f

let d = new FileInfo(Path.Combine(projectRoot, basedir))
let fname = d.FullName

let makeRelPath (root: string) =
    let rootLen = root.Length
    if rootLen <= 0 then id
    else
        let root', rootLen' =
            if root.[rootLen - 1] = Path.DirectorySeparatorChar then
                root, rootLen
            else
                root + (System.String (Path.DirectorySeparatorChar, 1)), rootLen + 1

        fun (path: string) ->
            if path.StartsWith root' then
                path.Substring rootLen'
            else
                path

let makeRoutes basedir (Filelist files) =
    
    files |> List.map (fun f ->
        let fullName = f |> File.getFullName
        fullName, fullName |> makeRelPath basedir)

let toRoutes projectRoot fileset =
    let filelist = fileset |> Fileset.toFileList projectRoot
    let (Fileset ({BaseDir = basedir'}, _)) = fileset

    let basedir = basedir' |> function | None -> projectRoot | Some s -> Path.Combine(projectRoot, s)
    let baseFullPath = FileInfo(basedir).FullName
    in
    makeRoutes baseFullPath filelist

let xx = f |> toRoutes "."

let fn = fileset {
    includes "samples/a/**/"
}

let yy = fn |> toRoutes "."