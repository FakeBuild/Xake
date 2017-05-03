namespace Xake.Tasks.Dotnet

[<AutoOpen>]
module MsbuildImpl =

    open System.IO
    open Xake
    open Xake.SystemTasks
    open DotNetTaskTypes

    // Sln (msbuild/xbuild) task settings
    type MSBuildSettingsType = {
        /// Build file location
        BuildFile: string
        /// Build these targets.
        Target: string list
        /// Set or override project-level properties.
        Property: (string*string) list
        /// Maximum number of concurrent processes. Some 0 - to use number of processors on the computer.
        MaxCpuCount: int option
        /// The version of MSBuild toolset (tasks, targets etc) to use during the build.
        ToolsVersion: string option
        /// Output this amount of information. All MSBuild output is considered Infomation so is displayed when logging level is Chatty.
        Verbosity: MsbVerbosity
        /// Insert command-line settings from file
        RspFile: string option
        // Build fails on compile error.
        FailOnError: bool
    }

    // Default settings for Sln (MSBuild) task
    let MSBuildSettings = {
        BuildFile = null
        Target = []
        Property = []
        MaxCpuCount = None
        ToolsVersion = None
        Verbosity = Normal
        RspFile = None
        FailOnError = true
    }

    let MSBuild (settings:MSBuildSettingsType) =

        action {
            let! dotnetFwk = getVar "NETFX"
            let fwkInfo = DotNetFwk.locateFramework dotnetFwk

            let pfx = "msbuild"

            let verbosityKey = function | Quiet -> "q" | Minimal -> "m" | Normal -> "n" | Detailed -> "d" | Diag -> "diag"

            let args =
                seq {
                    yield "/nologo"
                    yield settings.BuildFile

                    match settings.Target with
                        | [] -> ()
                        | lst -> yield "/t:" + (lst |> String.concat ";")

                    match settings.Property with
                        | [] -> ()
                        | lst -> yield "/property:" + (lst |> List.map (fun (k,v) -> sprintf "%s=%s" k v) |> String.concat ";")

                    match settings.MaxCpuCount with
                        | None -> ()
                        | Some 0 -> yield "/m"
                        | Some n -> yield sprintf "/m:%i" n

                    if Option.isSome settings.ToolsVersion then yield sprintf "/toolsversion:%s" (Option.get settings.ToolsVersion)
                    if Option.isSome settings.RspFile then yield sprintf "@%s" (Option.get settings.RspFile)

                    match settings.Verbosity with
                        | Normal -> ()
                        | v -> yield sprintf "/verbosity:%s" (verbosityKey v)
                }

            do! trace Info "%s making '%s' using framework '%s'" pfx settings.BuildFile fwkInfo.Version
            do! trace Debug "Command line: '%A'" args

            let options = {
                SysOptions.Default with
                    Command = fwkInfo.MsbuildTool
                    Args = args
                    LogPrefix = pfx
                    StdOutLevel = fun _ -> Level.Info
                    ErrOutLevel = Impl.levelFromString Level.Verbose
                }
            let! exitCode = _system options

            do! trace Info "%s done '%s'" pfx settings.BuildFile
            if exitCode <> 0 then
                do! trace Error "%s ('%s') failed with exit code '%i'" pfx settings.BuildFile exitCode
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" pfx
            ()
        }

