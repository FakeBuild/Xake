// xake build file
#r @"bin/Debug/Xake.Core.dll"

open Xake

let fsc = """C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\fsc.exe"""

do xake {XakeOptions with FileLog = "build.log"; ConLogLevel = Verbosity.Chatty } {

    rules [
        "main"  <== ["build"]
        "build" <== ["bin/Xake.Core.dll"]

        "clean" => action {
            do! rm ["bin/Xake.Core.dll"]
        }

        "bin/Xake.Core.dll" *> fun file -> action {

            // TODO --doc:..\bin\Xake.Core.XML --- multitarget rule!
            
            let sources = fileset {
                basedir "core"
                includes "Logging.fs"
                includes "Pickler.fs"
                includes "Fileset.fs"
                includes "Types.fs"
                includes "ArtifactUtil.fs"
                includes "CommonLib.fs"
                includes "Database.fs"
                includes "Action.fs"
                includes "WorkerPool.fs"
                includes "Progress.fs"
                includes "XakeScript.fs"
                includes "CommonTasks.fs"
                includes "FileTasks.fs"
                includes "ResourceFileset.fs"
                includes "DotNetFwk.fs"
                includes "DotnetTasks.fs"
                includes "VersionInfo.fs"
                includes "AssemblyInfo.fs"
                includes "Program.fs"
            }

            let! options = getCtxOptions()
            let getFiles = toFileList options.ProjectRoot

            let (Filelist sourceFiles) = sources |> toFileList options.ProjectRoot

            do! needFiles (Filelist sourceFiles)

            let (settings,files,refs) =
                (
                    ["--target:library"; "--define:TRACE"; "--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"],
                    ["Logging.fs"; "Pickler.fs"; "Fileset.fs"; "Types.fs"; "ArtifactUtil.fs"; "CommonLib.fs"; "Database.fs"; "Action.fs"; "WorkerPool.fs"; "Progress.fs"; "XakeScript.fs"; "CommonTasks.fs"; "FileTasks.fs"; "ResourceFileset.fs"; "DotNetFwk.fs"; "DotnetTasks.fs"; "VersionInfo.fs"; "AssemblyInfo.fs"; "Program.fs"],
                    ["System.dll"; "System.Core.dll"; "System.Windows.Forms.dll"]
                )

            let! exitcode = system fsc <| ["-o:" + file.FullName; ""] @ settings @ (sourceFiles |> List.map (fun f -> f.FullName)) @ (refs |> List.map (fun n -> "/r:" + n))

            do! writeLog Command "job done %A" exitcode
        }

        "bin/Xake.Core1.dll" *> fun file -> action {

            // TODO --doc:..\bin\Xake.Core.XML --- multitarget rule!
            
            let sources = fileset {
                basedir "core"
                includes "Logging.fs"
                includes "Pickler.fs"
                includes "Fileset.fs"
                includes "Types.fs"
                includes "ArtifactUtil.fs"
                includes "CommonLib.fs"
                includes "Database.fs"
                includes "Action.fs"
                includes "WorkerPool.fs"
                includes "Progress.fs"
                includes "XakeScript.fs"
                includes "CommonTasks.fs"
                includes "FileTasks.fs"
                includes "ResourceFileset.fs"
                includes "DotNetFwk.fs"
                includes "DotnetTasks.fs"
                includes "VersionInfo.fs"
                includes "AssemblyInfo.fs"
                includes "Program.fs"
            }

            do! Fsc {
                FscSettings with
                    Out = file
                    Src = sources
                    RefGlobal = ["System.dll"; "System.Core.dll"; "System.Windows.Forms.dll"]
                    Define = ["TRACE"]
                }

        }
    ]

}
