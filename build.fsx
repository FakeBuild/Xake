#r "paket:
    nuget Xake ~> 1.1 prerelease //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#endif

open Xake
open Xake.Tasks

let frameworks = ["netstandard2.0"; "net46"]
let libtargets =
    [ for t in frameworks do
      for e in ["dll"; "xml"]
        -> sprintf "out/%s/Xake.%s" t e
    ]

let getVersion () = recipe {
    let! verVar = getVar("VER")
    let! ver = getEnv("VER")
    return verVar |> Option.defaultValue (ver |> Option.defaultValue "0.0.1")
}

let makePackageName () = recipe {
    let! ver = getVersion()
    let! verSuffix =
        getVar("SUFFIX")
        |> Recipe.map (
            function
            | None -> "-alpha"
            | Some "" -> "" // this is release!
            | Some s -> "-" + s
            )

    return sprintf "Xake.%s%s.nupkg" ver verSuffix
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

        "build" <== libtargets
        "clean" => rm {dir "out"}

        "test" => recipe {
            do! alwaysRerun()

            let! where =
              getVar("FILTER")
              |> Recipe.map (function |Some clause -> ["--filter"; sprintf "Name~\"%s\"" clause] | None -> [])

            // in case of travis only run tests for standard runtime, eventually will add more
            let! limitFwk = getEnv("TRAVIS") |> Recipe.map (function | Some _ -> ["-f:netcoreapp2.0"] | _ -> [])

            do! dotnet <| ["test"; "src/tests"; "-c"; "Release"] @ where @ limitFwk
        }

        libtargets *..> recipe {

            let! allFiles
                = getFiles <| fileset {
                    basedir "src/core"
                    includes "Xake.fsproj"
                    includes "**/*.fs"
                }

            do! needFiles allFiles
            let! version = getVersion()

            for framework in frameworks do
                do! dotnet
                        [
                            "build"
                            "src/core"
                            "/p:Version=" + version
                            "--configuration"; "Release"
                            "--framework"; framework
                            "--output"; "../../out/" + framework
                            "/p:DocumentationFile=Xake.xml"
                        ]
        }
    ]

    (* Nuget publishing rules *)
    rules [
        "pack" => recipe {
            let! package_name = makePackageName ()
            do! need ["out" </> package_name]
        }

        "out/Xake.(ver:*).nupkg" ..> recipe {
            let! ver = getRuleMatch("ver")
            do! dotnet
                  [
                      "pack"; "src/core"
                      "-c"; "Release"
                      "/p:Version=" + ver
                      "--output"; "../../out/"
                      "/p:DocumentationFile=Xake.xml"
                  ]
        }

        // push need pack to be explicitly called in advance
        "push" => recipe {

            let! package_name = makePackageName ()

            let! nuget_key = getEnv("NUGET_KEY")
            do! dotnet
                  [
                    "nuget"; "push"
                    "out" </> package_name
                    "--source"; "https://www.nuget.org/api/v2/package"
                    "--api-key"; nuget_key |> Option.defaultValue ""
                  ]
        }
    ]
}

