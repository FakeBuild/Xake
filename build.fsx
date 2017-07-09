// xake build file

#r @"packages/Xake/tools/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

let TestsAssembly, CoreAssembly = "bin/XakeLibTests.dll", "bin/Xake.dll"
let (=?) value deflt = match value with |Some v -> v |None -> deflt

let makePackageName () = recipe {
    let! ver = getEnv("VER")
    return sprintf "Xake.%s.nupkg" (ver =? "0.0.1")
}
let paket arglist = recipe {
    do! shell {
        useclr
        cmd ".paket/paket.exe"
        args arglist
        failonerror
        } |> Recipe.Ignore
}

let nunitConsoleExe = "packages/NUnit.ConsoleRunner/tools/nunit3-console.exe" |> File.make |> File.getFullName

do xakeScript {
    var "NETFX-TARGET" "4.5"
    filelog "build.log" Verbosity.Diag
    // consolelog Verbosity.Normal

    rules [
        "main"  => recipe {
            do! need ["build"]
            do! need ["test"]
            }

        "build" <== [TestsAssembly; CoreAssembly]
        "clean" => rm {file "bin/*.*"}

        "test" => recipe {
            do! alwaysRerun()
            do! need[TestsAssembly]

            let! where = getVar("WHERE")
            let whereArgs = where |> function | Some clause -> ["--where"; clause] | None -> []

            do! (shell {
                useclr
                cmd nunitConsoleExe
                args (["XakeLibTests.dll"] @ whereArgs)
                failonerror
                workdir "bin"                
                }) |> Recipe.Ignore
        }

        ("bin/FSharp.Core.dll") ..> (WhenError ignore <| recipe {
                do! copyFrom "packages/FSharp.Core/lib/net40/FSharp.Core.dll"
                do! copy {todir "bin"; file "packages/FSharp.Core/lib/net40/FSharp.Core.*data"}
            })

        ("bin/nunit.framework.dll") ..> copyFrom "packages/NUnit/lib/nunit.framework.dll"

        CoreAssembly ..> recipe {

            // TODO multitarget rule!
            let! target = getTargetFile()
            let xml = target.FullName -. "xml"

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
                includes "RecipeBuilder.fs"
                includes "RecipeFunctions.fs"
                includes "WorkerPool.fs"
                includes "Progress.fs"
                includes "ExecTypes.fs"
                includes "DependencyAnalysis.fs"
                includes "ExecCore.fs"
                includes "XakeScript.fs"
                includes "ScriptFuncs.fs"
                includes "ResourceFileset.fs"
                includes "ProcessExec.fs"
                includes "FileTasksImpl.fs"
                includes "DotNetFwk.fs"
                includes "DotnetTasks.fs"
                includes "Tasks/**/*.fs"
                includes "VersionInfo.fs"
                includes "AssemblyInfo.fs"
                includes "Program.fs"
            }

            do! Fsc {
                FscSettings with
                    Src = sources
                    Ref = !! "bin/FSharp.Core.dll"
                    RefGlobal = ["System.dll"; "System.Core.dll"; "System.Windows.Forms.dll"]
                    Define = ["TRACE"]
                    CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"; "--doc:" + xml]
            }

        }

        TestsAssembly ..> Fsc {
            FscSettings with
                Src = !! "XakeLibTests/*.fs"
                Ref = !! "bin/FSharp.Core.dll" + "bin/nunit.framework.dll" + CoreAssembly
                RefGlobal = ["System.dll"; "System.Core.dll"]
                Define = ["TRACE"]
        }
    ]

    (* Nuget publishing rules *)
    rules [
        "nuget-pack" => recipe {
            let! package_name = makePackageName ()
            do! need [package_name]
        }

        "Xake.(ver:*).nupkg" ..> recipe {
            do! need [CoreAssembly]
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

