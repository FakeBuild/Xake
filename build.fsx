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

            do! Fsc {
                FscSettings with
                    Out = file
                    Src = sources
                    RefGlobal = ["System.dll"; "System.Core.dll"; "System.Windows.Forms.dll"]
                    Define = ["TRACE"]
                    CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"]
                }

        }
    ]

}
