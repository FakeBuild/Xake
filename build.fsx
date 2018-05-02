// xake build file

//#r @"packages/Xake/tools/Xake.dll"
//#r @"bin/Debug/Xake.dll"
#r "core/bin/Release/net46/Xake.dll"

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

let TestsAssembly, XakeDll, XakeXml = "bin/XakeLibTests.dll", "bin/netstandard2.0/Xake.dll", "bin/netstandard2.0/Xake.xml"
let (=?) value deflt = match value with |Some v -> v |None -> deflt

let getVer () = recipe {
    let! verVar = getVar("VER")
    let! ver = getEnv("VER")
    return verVar =? (ver =? "0.0.1")
}

let makePackageName () = recipe {
    let! ver = getVer()
    return sprintf "Xake.%s.nupkg" ver
}

let paket arglist = recipe {
    do! shell {
        useclr
        cmd ".paket/paket.exe"
        args arglist
        failonerror
        } |> Recipe.Ignore
}

let dotnet arglist = recipe {
    do! shell {
        cmd "dotnet"
        args arglist
        failonerror
        } |> Recipe.Ignore
}

let sourceFiles =
    fileset {
        basedir "Core"
        includes "VersionInfo.fs"
        includes "**/*.fs"; includes "Xake.fsproj"
    }

do xakeScript {
    filelog "build.log" Verbosity.Diag
    // consolelog Verbosity.Normal

    rules [
        "main"  => recipe {
            do! need ["build"]
            // do! need ["test"]
            }

        "build" <== [TestsAssembly; XakeDll]
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

        // ["bin/FSharp.Core.dll"
        //  "bin/FSharp.Core.optdata"
        //  "bin/FSharp.Core.sigdata"
        // ] *..> copy {todir "bin"; file "packages/FSharp.Core/lib/net40/FSharp.Core.*"}

        // ("bin/nunit.framework.dll") ..> copyFrom "packages/NUnit/lib/nunit.framework.dll"

        [XakeDll; XakeXml] *..> recipe {

            let! allFiles = getFiles sourceFiles
            do! needFiles allFiles

            do! dotnet ["build"; "Core"; "-c"; "Release"]
        }

        TestsAssembly ..> Fsc {
            FscSettings with
                Src = !! "XakeLibTests/*.fs"
                Ref = !! "bin/FSharp.Core.dll" + "bin/nunit.framework.dll" + XakeDll
                RefGlobal = ["System.dll"; "System.Core.dll"]
                Define = ["TRACE"]
        }

        "core/VersionInfo.cs" ..> recipe {
            
            let! version = getVer()
            do! writeText <| sprintf "module Xake.Const [<Literal>]  let internal Version = \"%s\"" version
        }
    ]

    (* Nuget publishing rules *)
    rules [
        "nuget-pack" => recipe {
            let! package_name = makePackageName ()
            do! need ["bin" </> package_name]
        }

        "bin/Xake.(ver:*).nupkg" ..> recipe {
            do! need [XakeDll; XakeXml]
            let! ver = getRuleMatch("ver")

            // "module Xake.Const [<Literal>]  let internal Version = \"$VER.$TRAVIS_BUILD_NUMBER\"" >./core/VersionInfo.fs

            // TODO set version in assembly-info
            do! dotnet ["pack"; "-c"; "Release"; "/p:Version=" + ver ]
        }

        "nuget-push" => recipe {

            let! package_name = makePackageName ()
            do! need [package_name]

            let! nuget_key = getEnv("NUGET_KEY")
            do! paket
                  [
                    "push"
                    "--url"; "https://www.nuget.org/api/v2/package"
                    package_name
                    "--api-key"; nuget_key =? ""
                  ]
        }
    ]
}

