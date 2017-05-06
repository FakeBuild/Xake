namespace Xake.Tasks.Dotnet

[<AutoOpen>]
module FscImpl =

    open System.IO
    open Xake
    open Xake.SystemTasks
    open DotNetTaskTypes

    /// <summary>
    /// Fsc (F# compiler) task settings.
    /// </summary>
    type FscSettingsType = {
        /// Limits which platforms this code can run on. The default is anycpu.
        Platform: TargetPlatform
        /// Specifies the format of the output file.
        Target: TargetType
        /// Specifies the output file name (default: base name of file with main class or first file).
        Out: File
        /// Source files.
        Src: Fileset
        /// References metadata from the specified assembly files.
        Ref: Fileset
        /// References the specified assemblies from GAC.
        RefGlobal: string list
        /// Embeds the specified resource.
        Resources: ResourceFileset list
        /// Defines conditional compilation symbols.
        Define: string list
        /// Target .NET framework
        TargetFramework: string
        /// Use specific FSC compiler version (only dotnet)
        FscVersion: string option
        /// Custom command-line arguments
        CommandArgs: string list
        /// Build fails on compile error.
        FailOnError: bool
        /// Do not reference the default CLI assemblies by default
        NoFramework: bool

        Tailcalls: bool
    }

        /// <summary>
    /// Default settings for Fsc task.
    /// </summary>
    let FscSettings = {
        FscSettingsType.Platform = AnyCpu
        FscSettingsType.Target = Auto
        Out = File.undefined
        Src = Fileset.Empty
        Ref = Fileset.Empty
        RefGlobal = []
        Resources = []
        Define = []
        TargetFramework = null
        FscVersion = None
        CommandArgs = []
        FailOnError = true
        NoFramework = false

        Tailcalls = true
    }

    /// F# compiler task
    let Fsc (settings:FscSettingsType) =

        recipe {
            let! ctx = getCtx()
            let logger = ctx.RootLogger

            let! options = getCtxOptions()
            let getFiles = toFileList options.ProjectRoot

            let! contextTargetFile = getTargetFile()
            let outFile = if settings.Out = File.undefined then contextTargetFile else settings.Out

            let resinfos = settings.Resources |> List.collect (Impl.collectResInfo options.ProjectRoot) |> List.map Impl.compileResxFiles
            let resfiles =
                List.ofSeq <|
                query {
                    for (_,file,istemp) in resinfos do
                        where (not istemp)
                        select (file)
                }

            let (Filelist src)  = settings.Src |> getFiles
            let (Filelist refs) = settings.Ref |> getFiles

            do! needFiles (Filelist (src @ refs @ resfiles))

            let! globalTargetFwk = getVar "NETFX-TARGET"
            let targetFramework =
                match settings.TargetFramework, globalTargetFwk with
                | s, _ when not (System.String.IsNullOrWhiteSpace s) -> s
                | _, Some s when s <> "" -> s
                | _ -> null

            logger.Log Debug "targetFramework: %s" targetFramework

            let (globalRefs,noframework) =
                let mapfn = (+) "/r:"
                match targetFramework with
                | null ->
                    // TODO provide an option for user to explicitly specify all grefs (currently csc.rsp is used)
                    (settings.RefGlobal |> List.map mapfn), false
                | tgt ->
                    let fwk = Some tgt |> DotNetFwk.locateFramework in
                    let lookup = DotNetFwk.locateAssembly fwk
                    do logger.Log Debug "Found fwk %A" fwk

                    ("mscorlib.dll" :: settings.RefGlobal |> List.map (lookup >> mapfn)), true

            let args =
                seq {
                    yield "/nologo"

                    yield "/target:" + Impl.targetStr outFile.Name settings.Target
                    //yield "/platform:" + Impl.platformStr settings.Platform

                    if settings.NoFramework || noframework then
                        yield "--noframework"

                    if outFile <> File.undefined then
                        yield sprintf "/out:%s" (File.getFullName outFile)

                    if not (List.isEmpty settings.Define) then
                        yield "/define:" + (settings.Define |> String.concat ";")

                    yield! src |> List.map (fun f -> f.FullName)

                    yield! refs |> List.map ((fun f -> f.FullName) >> (+) "/r:")
                    yield! globalRefs

                    yield! resinfos |> List.map (fun(name,file,_) -> sprintf "/res:%s,%s" file.FullName name)
                    yield! settings.CommandArgs
                }

            let! dotnetFwk = getVar "NETFX"
            let fwkInfo = DotNetFwk.locateFramework dotnetFwk

            let! fscVer = getVar "FSCVER"
            let fsc = 
                match fwkInfo.FscTool ([settings.FscVersion; fscVer] |> Impl.coalesce) with
                | Some tool -> tool
                | None -> ""
            if fsc = "" then
                do! trace Error "('%s') failed: F# compiler not found" outFile.Name
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" outFile.Name

            let args = args |> Seq.map Impl.escapeArgument
            do! trace Info "compiling '%s' using framework '%s'" outFile.Name fwkInfo.Version
            do! trace Debug "Command line: '%s %s'" fsc (args |> String.concat "\r\n\t")

            let options = {
                ShellOptions.Default with
                    Command = fsc
                    Args = args
                    LogPrefix = "[FSC] "
                    StdOutLevel = fun _ -> Level.Verbose
                    ErrOutLevel = Impl.levelFromString Level.Verbose
                    EnvVars = fwkInfo.EnvVars
                }
            let! exitCode = _system options

            do! trace Verbose "Deleting temporary files"
            seq {
                yield! query {
                    for (_,file,istemp) in resinfos do
                        where istemp
                        select file.FullName
                }
            }
            |> Seq.iter File.Delete

            if exitCode <> 0 then
                do! trace Error "('%s') failed with exit code '%i'" outFile.Name exitCode
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" outFile.Name
        }
