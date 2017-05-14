namespace Xake.Tasks.Dotnet

[<AutoOpen>]
module CscImpl =

    open System.IO
    open Xake

    type CscSettingsType = {
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
        /// Allows unsafe code.
        Unsafe: bool
        /// Target .NET framework
        TargetFramework: string
        /// Custom command-line arguments
        CommandArgs: string list
        /// Build fails on compile error.
        FailOnError: bool
        /// Path to csc executable
        CscPath: string option
    }

    /// Default setting for CSC task so that you could only override required settings
    let CscSettings = {
        CscSettingsType.Platform = AnyCpu
        Target = Auto    // try to resolve the type from name etc
        Out = File.undefined
        Src = Fileset.Empty
        Ref = Fileset.Empty
        RefGlobal = []
        Resources = []
        Define = []
        Unsafe = false
        TargetFramework = null
        CommandArgs = []
        FailOnError = true
        CscPath = None
    }

    /// C# compiler task
    let Csc (settings:CscSettingsType) =

        action {
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
                | s, _ when not <| System.String.IsNullOrWhiteSpace(s) -> s
                | _, Some s when s <> "" -> s
                | _ -> null

            let (globalRefs,nostdlib,noconfig) =
                match targetFramework with
                | null ->
                    let mapfn = (+) "/r:"
                    // TODO provide an option for user to explicitly specify all grefs (currently csc.rsp is used)
                    (settings.RefGlobal |> List.map mapfn), false, false
                | tgt ->
                    let fwk = Some tgt |> DotNetFwk.locateFramework in
                    let lookup = DotNetFwk.locateAssembly fwk
                    let mapfn = lookup >> ((+) "/r:")

                    ("mscorlib.dll" :: settings.RefGlobal |> List.map mapfn), true, true

            let args =
                seq {
                    yield "/nologo"

                    yield "/target:" + Impl.targetStr outFile.Name settings.Target
                    yield "/platform:" + Impl.platformStr settings.Platform

                    if settings.Unsafe then
                        yield "/unsafe"

                    if nostdlib then
                        yield "/nostdlib+"

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

// TODO for short args this is ok, otherwise use rsp file --    let commandLine = args |> escapeAndJoinArgs
            let rspFile = Path.GetTempFileName()
            File.WriteAllLines(rspFile, args |> Seq.map Impl.escapeArgument |> List.ofSeq)
            let commandLineArgs =
                seq {
                    if noconfig then
                        yield "/noconfig"
                    yield "@" + rspFile
                    }
            let cscTool = settings.CscPath |> function | Some v -> v | _ -> fwkInfo.CscTool

            do! trace Info "compiling '%s' using framework '%s'" outFile.Name fwkInfo.Version
            do! trace Debug "Command line: '%s %s'" cscTool (args |> Seq.map Impl.escapeArgument |> String.concat "\r\n\t")

            let! exitCode = Impl._system cscTool commandLineArgs fwkInfo.EnvVars "[CSC] "

            do! trace Level.Verbose "Deleting temporary files"
            seq {
                yield rspFile

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

    (* csc options builder *)
    type CscSettingsBuilder() =

        [<CustomOperation("platform")>]  member this.Platform(s:CscSettingsType, value) =    {s with Platform = value}
        [<CustomOperation("target")>]    member this.Target(s:CscSettingsType, value) =    {s with Target = value}
        [<CustomOperation("targetfwk")>] member this.TargetFwk(s:CscSettingsType, value) = {s with TargetFramework = value}
        [<CustomOperation("out")>]       member this.OutFile(s:CscSettingsType, value) =   {s with Out = value}
        [<CustomOperation("src")>]       member this.SrcFiles(s:CscSettingsType, value) =  {s with Src = value}

        [<CustomOperation("ref")>]       member this.Ref(s:CscSettingsType, value) =         {s with Ref = s.Ref + value}
        [<CustomOperation("refif")>]     member this.Refif(s:CscSettingsType, cond, (value:Fileset)) = {s with Ref = s.Ref +? (cond,value)}

        [<CustomOperation("refs")>]      member this.Refs(s:CscSettingsType, value) =        {s with Ref = value}
        [<CustomOperation("grefs")>]     member this.RefGlobal(s:CscSettingsType, value) =   {s with RefGlobal = value}
        [<CustomOperation("resources")>] member this.Resources(s:CscSettingsType, value) =   {s with CscSettingsType.Resources = value :: s.Resources}
        [<CustomOperation("resourceslist")>] member this.ResourcesList(s:CscSettingsType, values) = {s with CscSettingsType.Resources = values @ s.Resources}

        [<CustomOperation("define")>]    member this.Define(s:CscSettingsType, value) =      {s with Define = value}
        [<CustomOperation("unsafe")>]    member this.Unsafe(s:CscSettingsType, value) =      {s with Unsafe = value}
        [<CustomOperation("cscpath")>]       member this.CscPath(s:CscSettingsType, value) =   {s with CscPath = Some value}

        member this.Bind(x, f) = f x
        member this.Yield(()) = CscSettings
        member this.For(x, f) = f x

        member this.Zero() = CscSettings
        member this.Run(s:CscSettingsType) = Csc s

    let csc = CscSettingsBuilder()
