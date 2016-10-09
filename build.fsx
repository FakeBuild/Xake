// xake build file

#r @"packages/Xake/tools/Xake.Core.dll"
//#r @"bin/Debug/Xake.Core.dll"

open Xake
open Xake.SystemTasks

let TestsAssembly = "bin/XakeLibTests.dll"
let (=?) value deflt = match value with |Some v -> v |None -> deflt

let DEF_VER = "0.0.1"

let nuget_exe args = system (useClr >> checkErrorLevel) "packages/NuGet.CommandLine/tools/NuGet.exe" args |> Action.Ignore

module nuget =
    module private impl =
        let newline = System.Environment.NewLine
        let wrapXml node value = sprintf "<%s>%s</%s>" node value node
        let wrapXmlNl node (value:string) =
            let attrs = value.Split([|newline|], System.StringSplitOptions.None) |> Seq.ofArray
            let content = attrs |> Seq.map ((+) "  ") |> String.concat newline
            sprintf "<%s>%s</%s>" node (newline + content + newline) node
        let toXmlStr (node,value) = wrapXml node value

    open impl

    let dependencies deps =
        "dependencies", newline
        + (deps |> List.map (fun (s,v) -> sprintf """<dependency id="%s" version="%s" />""" s v) |> String.concat newline)
        + newline

    let metadata = List.map toXmlStr >> String.concat newline >> wrapXmlNl "metadata"
    let files = List.map (fun(f,t) -> (f,t) ||> sprintf """<file src="%s" target="%s" />""") >> String.concat newline >> wrapXmlNl "files"
    let target t ff = ff |> List.map (fun file -> file,t)
    let package = String.concat newline >> wrapXmlNl "package" >> ((+) ("<?xml version=\"1.0\"?>" + newline))

do xake {ExecOptions.Default with ConLogLevel = Verbosity.Chatty } {
    var "NETFX-TARGET" "4.5"
    filelog "build.log" Verbosity.Diag

    rules [
        "main"  => action {
            do! need ["build"]
            do! need ["test"]
            }

        "build" <== [TestsAssembly; "bin/Xake.Core.dll"]
        "clean" => action {
            do! rm ["bin/*.*"]
        }

        "test" => action {
            do! need[TestsAssembly]
            do! system (useClr >> checkErrorLevel) "packages/NUnit.Runners/tools/nunit-console.exe" [TestsAssembly] |> Action.Ignore
        }

        ("bin/FSharp.Core.dll") *> fun outfile ->
            WhenError ignore <| action {
                do! copyFile "packages/FSharp.Core/lib/net40/FSharp.Core.dll" outfile.FullName
                do! copyFiles ["packages/FSharp.Core/lib/net40/FSharp.Core.*data"] "bin"
            }

        ("bin/nunit.framework.dll") *> fun outfile -> action {
            do! copyFile "packages/NUnit/lib/nunit.framework.dll" outfile.FullName
        }

        "bin/Xake.Core.dll" *> fun file -> action {

            // TODO multitarget rule!
            let xml = "bin/Xake.Core.XML" // file.FullName .- "XML"

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

        TestsAssembly *> fun file -> action {

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

    (* Nuget publishing rules *)
    rules [
        "nuget-pack" => action {

            let libFiles = ["bin/Xake.Core.dll"]
            do! need libFiles

            let! ver = getEnv("VER")

            let nuspec =
                nuget.package [
                    nuget.metadata [
                        "id", "Xake"
                        "version", ver =? DEF_VER
                        "authors", "OlegZee"
                        "owners", "OlegZee"
                        "projectUrl", "https://github.com/OlegZee/Xake"
                        "requireLicenseAcceptance", "false"
                        "description", "Xake build tool"
                        "releaseNotes", ""
                        "copyright", sprintf "Copyright %i" System.DateTime.Now.Year
                        "tags", "Xake F# Build"
                        nuget.dependencies []
                    ]
                    nuget.files (libFiles |> nuget.target "tools")
                ]

            let nuspec_file = "_.nuspec"
            do System.IO.Directory.CreateDirectory("nupkg") |> ignore
            do System.IO.File.WriteAllText(nuspec_file, nuspec)

            do! nuget_exe ["pack"; nuspec_file; "-OutputDirectory"; "nupkg" ]
        }

        "nuget-push" => action {

            do! need ["nuget-pack"]

            let! ver = getEnv("VER")
            let package_name = sprintf "Xake.%s.nupkg" (ver =? DEF_VER)

            let! nuget_key = getEnv("NUGET_KEY")
            do! nuget_exe ["push"; "nupkg" </> package_name; nuget_key =? ""; "-Source"; "https://www.nuget.org/api/v2/package"]
        }
    ]
}

