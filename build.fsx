#r "paket:
    nuget Xake ~> 1.0 prerelease //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#endif

open Xake
open Xake.Tasks

let XakeDll, XakeXml = "out/netstandard2.0/Xake.dll", "out/netstandard2.0/Xake.xml"
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

do xakeScript {
    filelog "build.log" Verbosity.Diag
    // consolelog Verbosity.Normal

    rules [
        "main"  => recipe {
            do! need ["build"]
            do! need ["test"]
            }

        "build" <== [XakeDll; XakeXml]
        "clean" => rm {file "out/*.*"}

        "test" => recipe {
            do! alwaysRerun()

            // let! where = getVar("WHERE")
            // let whereArgs = where |> function | Some clause -> ["--filter"; clause] | None -> []
            // TODO filters            

            do! dotnet ["test"; "src/tests"; "-c"; "Release"]
        }

        [XakeDll; XakeXml] *..> recipe {

            let! allFiles
                = getFiles <| fileset {
                    basedir "src/core"
                    includes "Xake.fsproj"
                    includes "VersionInfo.fs"
                    includes "**/*.fs"
                }

            do! needFiles allFiles

            // todo set output folder from script (in here) to increase cohesion
            do! dotnet ["build"; "src/core"; "-c"; "Release"]
        }

        "src/core/VersionInfo.fs" ..> recipe {
            
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

        "out/Xake.(ver:*).nupkg" ..> recipe {
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

