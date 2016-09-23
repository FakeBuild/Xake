namespace Xake

open System.IO
open System.Resources
open Xake

[<AutoOpen>]
module DotNetTaskTypes =

    // CSC task and related types
    type TargetType = |Auto |AppContainerExe |Exe |Library |Module |WinExe |WinmdObj
    type TargetPlatform = |AnyCpu |AnyCpu32Preferred |ARM | X64 | X86 |Itanium
    type CscSettingsType =
        {
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
        }
    // see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx
    // defines, optimize, warn, debug, platform

    // ResGen task and its settings
    type ResgenSettingsType = {

        Resources: ResourceFileset list
        TargetDir: System.IO.DirectoryInfo
        UseSourcePath: bool

        // TODO single file mode
        // TODO extra command-line args
    }

    type MsbVerbosity = | Quiet | Minimal | Normal | Detailed | Diag

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
        /// Custom command-line arguments
        CommandArgs: string list
        /// Build fails on compile error.
        FailOnError: bool
        /// Do not reference the default CLI assemblies by default
        NoFramework: bool

        Tailcalls: bool
    }

[<AutoOpen>]
module DotnetTasks =

    open Xake.SystemTasks

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
    }

    module internal Impl =
        begin
        /// Escapes argument according to CSC.exe rules (see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx)
        let escapeArgument (str:string) =
            let escape c s =
                match c,s with
                | '"',  (b,    str) -> (true,  '\\' :: '\"' ::    str)
                | '\\', (true, str) -> (true,  '\\' :: '\\' :: str)
                | '\\', (false,str) -> (false, '\\' :: str)
                | c,    (b,    str) -> (false, c :: str)

            if str |> String.exists (fun c -> c = '"' || c = ' ') then
                let ca = str.ToCharArray()
                let res = Array.foldBack escape ca (true,['"'])
                "\"" + System.String(res |> snd |> List.toArray)
            else
                str

        let isEmpty str = System.String.IsNullOrWhiteSpace(str)

        /// Gets the path relative to specified root path
        let getRelative (root:string) (path:string) =

            // TODO reimplement and test

            if isEmpty root then path
            elif path.ToLowerInvariant().StartsWith (root.ToLowerInvariant()) then
                // cut the trailing "\"
                let d = if root.Length < path.Length then 1 else 0
                path.Substring(root.Length + d)
            else
                path

        /// Makes resource name given the file name
        let makeResourceName (options:ResourceSetOptions) baseDir resxfile =

            let baseName = Path.GetFileName(resxfile)

            let baseName =
                match options.DynamicPrefix,baseDir with
                | true, Some dir ->
                    let path = Path.GetDirectoryName(resxfile) |> getRelative (Path.GetFullPath(dir))
                    if not <| isEmpty path then
                        path.Replace(Path.DirectorySeparatorChar, '.').Replace(':', '.') + "." + baseName
                    else
                        baseName
                | _ ->
                    baseName

            match options.Prefix with
                | Some prefix -> prefix + "." + baseName
                | _ -> baseName

        let compileResx (resxfile:File) (rcfile:File) =
            use resxreader = new System.Resources.ResXResourceReader (resxfile.FullName)
            resxreader.BasePath <- File.getDirName resxfile

            use writer = new ResourceWriter (rcfile.FullName)

            // TODO here we have deal with types somehow because we are running conversion under framework 4.5 but target could be 2.0
            writer.TypeNameConverter <-
                fun(t:System.Type) ->
                    t.AssemblyQualifiedName.Replace("4.0.0.0", "2.0.0.0")

            let reader = resxreader.GetEnumerator()
            while reader.MoveNext() do
                writer.AddResource (reader.Key :?> string, reader.Value)
            writer.Generate()

        let collectResInfo pathRoot = function
            |ResourceFileset (o,Fileset (fo,fs)) ->
                let mapFile file =
                    let resname = makeResourceName o fo.BaseDir (File.getFullName file) in
                    (resname,file)

                let (Filelist l) = Fileset (fo,fs) |> (toFileList pathRoot) in
                l |> List.map mapFile

        let endsWith e (str:string) = str.EndsWith (e, System.StringComparison.OrdinalIgnoreCase)
        let (|EndsWith|_|) e str = if endsWith e str then Some () else None

        let compileResxFiles = function
            | (res,(file:File)) when file |> File.getFileName |> endsWith ".resx" ->
                let tempfile = Path.GetTempFileName() |> File.make
                do compileResx file tempfile
                (Path.ChangeExtension(res,".resources"),tempfile,true)
            | (res,file) ->
                (res,file,false)

        let resolveTarget  =
            function
            | EndsWith ".dll" -> Library
            | EndsWith ".exe" -> Exe
            | _ -> Library

        let rec targetStr fileName = function
            |AppContainerExe -> "appcontainerexe" |Exe -> "exe" |Library -> "library" |Module -> "module" |WinExe -> "winexe" |WinmdObj -> "winmdobj"
            |Auto -> fileName |> resolveTarget |> targetStr fileName

        let platformStr = function
            |AnyCpu -> "anycpu" |AnyCpu32Preferred -> "anycpu32preferred" |ARM -> "arm" | X64 -> "x64" | X86 -> "x86" |Itanium -> "itanium"

        /// Parses the compiler output and returns messageLevel
        let levelFromString defaultLevel (text:string) :Level =
            if text.IndexOf "): warning " > 0 then Level.Warning
            else if text.IndexOf "): error " > 0 then Level.Error
            else defaultLevel

        end // end of Impl module

    /// C# compiler task
    let Csc (settings:CscSettingsType) =

        let outFile = settings.Out

        action {
            let! options = getCtxOptions()
            let getFiles = toFileList options.ProjectRoot

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
                | s, _ when s <> null && s <> "" -> s
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
                    let mapfn = (fun name -> "/r:" + (lookup name))

                    //do! writeLog Info "Using libraries from %A" fwk.AssemblyDirs

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
            let commandLine = commandLineArgs |> String.concat " "

            do! trace Info "compiling '%s' using framework '%s'" outFile.Name fwkInfo.Version
            do! trace Debug "Command line: '%s %s'" fwkInfo.CscTool (args |> Seq.map Impl.escapeArgument |> String.concat "\r\n\t")

            let options = {
                SystemTasks.SysOptions.Default with
                    LogPrefix = "[CSC] "
                    StdOutLevel = fun _ -> Level.Verbose
                    ErrOutLevel = Impl.levelFromString Level.Verbose
                    EnvVars = fwkInfo.EnvVars
                }
            let! exitCode = SystemTasks._system options fwkInfo.CscTool commandLine

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

        member this.Bind(x, f) = f x
        member this.Yield(()) = CscSettings
        member this.For(x, f) = f x

        member this.Zero() = CscSettings
        member this.Run(s:CscSettingsType) = Csc s

    let csc = CscSettingsBuilder()

    /// Generates binary resource files from resx, txt etc

    let ResgenSettings = {
        Resources = [Empty]
        TargetDir = System.IO.DirectoryInfo "."
        UseSourcePath = true
    }

    let ResGen (settings:ResgenSettingsType) =

        // TODO rewrite everything, it's just demo code
        let resgen baseDir (options:ResourceSetOptions) (resxfile:string) =
            use resxreader = new System.Resources.ResXResourceReader (resxfile)

            if settings.UseSourcePath then
                resxreader.BasePath <- Path.GetDirectoryName (resxfile)

            let rcfile =
                Path.Combine(
                    settings.TargetDir.FullName,
                    Path.ChangeExtension(resxfile, ".resource") |> Impl.makeResourceName options baseDir)

            use writer = new ResourceWriter (rcfile)

            let reader = resxreader.GetEnumerator()
            while reader.MoveNext() do
                writer.AddResource (reader.Key :?> string, reader.Value)

            rcfile

        action {
            //TODO
            //for r in settings.Resources do
            let r = settings.Resources.[0] in
                let (ResourceFileset (settings,fileset)) = r
                let (Fileset (options,_)) = fileset
                let! (Filelist files) = getFiles fileset

                do files |> List.map (fun f -> f.FullName) |> List.map (resgen options.BaseDir settings) |> ignore

            ()
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
                String.concat " " <|
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
            do! trace Debug "Command line: '%s'" args

            let options = {
                SystemTasks.SysOptions.Default with
                    LogPrefix = pfx
                    StdOutLevel = fun _ -> Level.Info
                    ErrOutLevel = Impl.levelFromString Level.Verbose
                }
            let! exitCode = args |> SystemTasks._system options fwkInfo.MsbuildTool

            do! trace Info "%s done '%s'" pfx settings.BuildFile
            if exitCode <> 0 then
                do! trace Error "%s ('%s') failed with exit code '%i'" pfx settings.BuildFile exitCode
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" pfx
            ()
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
        CommandArgs = []
        FailOnError = true
        NoFramework = false

        Tailcalls = true
    }

    /// F# compiler task
    let Fsc (settings:FscSettingsType) =

        let outFile = settings.Out

        action {
            let! options = getCtxOptions()
            let getFiles = toFileList options.ProjectRoot

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

            // TODO implement support for targeting various frameworks
            let! globalTargetFwk = getVar "NETFX-TARGET"
            let targetFramework =
                match settings.TargetFramework, globalTargetFwk with
                | s, _ when not (System.String.IsNullOrEmpty s) -> s
                | _, Some s when s <> "" -> s
                | _ -> null

            let (globalRefs,noframework) =
                match targetFramework with
                | null ->
                    let mapfn = (+) "/r:"
                    // TODO provide an option for user to explicitly specify all grefs (currently csc.rsp is used)
                    (settings.RefGlobal |> List.map mapfn), false
                | tgt ->
                    let fwk = Some tgt |> DotNetFwk.locateFramework in
                    let lookup = DotNetFwk.locateAssembly fwk
                    let mapfn = (fun name -> "/r:" + (lookup name))

                    //do! writeLog Info "Using libraries from %A" fwk.AssemblyDirs

                    ("mscorlib.dll" :: settings.RefGlobal |> List.map mapfn), true

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

            if Option.isNone fwkInfo.FscTool then
                do! trace Error "('%s') failed: F# compiler not found" outFile.Name
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" outFile.Name

            let fsc = Option.get fwkInfo.FscTool

            do! trace Info "compiling '%s' using framework '%s'" outFile.Name fwkInfo.Version
            do! trace Debug "Command line: '%s %s'" fsc (args |> Seq.map Impl.escapeArgument |> String.concat "\r\n\t")

            let options = {
                SystemTasks.SysOptions.Default with
                    LogPrefix = "[FSC] "
                    StdOutLevel = fun _ -> Level.Verbose
                    ErrOutLevel = Impl.levelFromString Level.Verbose
                    EnvVars = fwkInfo.EnvVars
                }
            let! exitCode = SystemTasks._system options fsc (args |> String.concat " ")

            do! trace Level.Verbose "Deleting temporary files"
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
