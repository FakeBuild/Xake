// xake build file
// boostrapping xake.core
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let file = System.IO.Path.Combine("packages", "Xake.Core.dll")
if not (System.IO.File.Exists file) then
    printf "downloading xake.core assembly..."; System.IO.Directory.CreateDirectory("packages") |> ignore
    let url = "https://github.com/OlegZee/Xake/releases/download/v0.3.16/Xake.Core.dll"
    use wc = new System.Net.WebClient() in wc.DownloadFile(url, file + "__"); System.IO.File.Move(file + "__", file)
    printfn ""

#r @"packages/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake

let mkdir = (System.IO.Directory.CreateDirectory:string->_) >> ignore

let systemClr cmd args =
    let cmd',args' = if Xake.Env.isUnix then "mono", cmd::args else cmd,args
    in system cmd' args'

do xake {ExecOptions.Default with Vars = ["NETFX-TARGET", "4.5"]; FileLog = "build.log"; ConLogLevel = Verbosity.Chatty } {

    rules [
        "all"  => action {
            do! need ["get-deps"]
            do! need ["build"]
            do! need ["test"]
            }

        "build" <== ["bin/XakeLibTests.dll"; "bin/Xake.Core.dll"]
        "clean" => action {
            do! rm ["bin/*.*"]
        }

        "get-deps" => action {
            let! exit_code = system ".paket/paket.bootstrapper.exe" []
            let! exit_code = system ".paket/paket.exe" ["install"]

            if exit_code <> 0 then
                failwith "Failed to install packages"
        }

        "test" => action {

            let! exit_code = systemClr "packages/NUnit.Runners/tools/nunit-console.exe" ["./bin/XakeLibTests.dll"]
            if exit_code <> 0 then
                failwith "Failed to test"
        }

        ("bin/FSharp.Core.dll") *> fun outfile -> action {
            let targetPath = "bin"
            do mkdir(targetPath)
            do! copyFile "packages/FSharp.Core/lib/net40/FSharp.Core.dll" outfile.FullName

            do! copyFiles ["packages/FSharp.Core/lib/net40/FSharp.Core.*data"] targetPath
        }

        ("bin/nunit.framework.dll") *> fun outfile -> action {
            do mkdir("bin")
            do! copyFile "packages/NUnit/lib/nunit.framework.dll" outfile.FullName
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
                includes "Env.fs"
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
                    Ref = !! "bin/FSharp.Core.dll"
                    RefGlobal = ["mscorlib.dll"; "System.dll"; "System.Core.dll"; "System.Windows.Forms.dll"]
                    Define = ["TRACE"]
                    CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"]
            }

        }

        "bin/XakeLibTests.dll" *> fun file -> action {

            // TODO --doc:..\bin\Xake.Core.XML --- multitarget rule!

            let sources = fileset {
                basedir "XakeLibTests"
                includes "ActionTests.fs"
                includes "FilesetTests.fs"
                includes "ScriptErrorTests.fs"
                includes "XakeScriptTests.fs"
                includes "MiscTests.fs"
                includes "StorageTests.fs"
                includes "FileTasksTests.fs"
                includes "ProgressTests.fs"
                includes "CommandLineTests.fs"
            }

            do! Fsc {
                FscSettings with
                    Out = file
                    Src = sources
                    Ref = !! "bin/FSharp.Core.dll" + "bin/nunit.framework.dll" + "bin/Xake.Core.dll"
                    RefGlobal = ["mscorlib.dll"; "System.dll"; "System.Core.dll"]
                    Define = ["TRACE"]
                    CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"]
            }

        }
    ]

}
