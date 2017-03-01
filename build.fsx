// xake build file

#r @"packages/Xake/tools/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake
open Xake.SystemTasks

let TestsAssembly = "bin/XakeLibTests.dll"
let (=?) value deflt = match value with |Some v -> v |None -> deflt

let DEF_VER = "0.0.1"
let makePackageName () = recipe {
    let! ver = getEnv("VER")
    return sprintf "Xake.%s.nupkg" (ver =? DEF_VER)
}
let paket args = system (useClr >> checkErrorLevel) ".paket/paket.exe" args |> Action.Ignore

do xake {ExecOptions.Default with ConLogLevel = Verbosity.Diag } {
    var "NETFX-TARGET" "4.5"
    filelog "build.log" Verbosity.Diag

    rules [
        "main"  => recipe {
            do! need ["build"]
            do! need ["test"]
            }

        "build" <== [TestsAssembly; "bin/Xake.Core.dll"]
        "clean" => rm ["bin/*.*"]

        "test" => recipe {
            do! alwaysRerun()
            do! need[TestsAssembly]
            do! system (useClr >> checkErrorLevel >> workingDir "bin") "packages/NUnit.ConsoleRunner/tools/nunit3-console.exe" ["XakeLibTests.dll"] |> Action.Ignore
        }

        ("bin/FSharp.Core.dll") ..> (WhenError ignore <| recipe {
                do! copyFrom "packages/FSharp.Core/lib/net40/FSharp.Core.dll"
                do! copyFiles ["packages/FSharp.Core/lib/net40/FSharp.Core.*data"] "bin"
            })

        ("bin/nunit.framework.dll") ..> copyFrom "packages/NUnit/lib/net40/nunit.framework.dll"

        "bin/Xake.Core.dll" ..> recipe {

            // TODO multitarget rule!
            let xml = "bin/Xake.Core.XML" // file.FullName .- "XML"
            let! file = getTargetFile()

            let sources = fileset {
                basedir "core"
                includes "Logging.fs"
                includes "Pickler.fs"
                includes "Env.fs"
                includes "Path.fs"
                includes "File.fsi"
                includes "File.fs"
                includes "Fileset.fs"
                includes "Types.fs"
                includes "CommonLib.fs"
                includes "Database.fs"
                includes "ActionBuilder.fs"
                includes "ActionFunctions.fs"
                includes "WorkerPool.fs"
                includes "Progress.fs"
                includes "ExecTypes.fs"
                includes "DependencyAnalysis.fs"
                includes "ExecCore.fs"
                includes "XakeScript.fs"
                includes "ScriptFuncs.fs"
                includes "SystemTasks.fs"
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
                    CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"; "--doc:" + xml]
            }

        }

        TestsAssembly ..> recipe {

            // TODO --doc:..\bin\Xake.Core.XML --- multitarget rule!
            let! file = getTargetFile()

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

    (* Nuget publishing rules *)
    rules [
        "nuget-pack" => recipe {
            let! package_name = makePackageName ()
            do! need [package_name]
        }

        "Xake.(ver:*).nupkg" ..> recipe {
            do! need ["bin/Xake.Core.dll"]
            let! ver = getRuleMatch("ver")
            do! paket ["pack"; "version"; ver; "output"; "." ]
        }

        "nuget-push" => recipe {

            let! package_name = makePackageName ()
            do! need [package_name]

            let! nuget_key = getEnv("NUGET_KEY")
            do! paket
                  [
                    "push"
                    "url"; "https://www.nuget.org/api/v2/package"
                    "file"; package_name
                    "apikey"; nuget_key =? ""
                  ]
        }
    ]
}

