namespace Xake

open System.Collections
open System.IO
open System.Resources
open Xake

[<AutoOpen>]
module DotNetTaskTypes =

    type FrameworkInfo = {Version: string; InstallPath: string}

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
            Out: Artifact
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

    type SlnVerbosity = | Quiet | Minimal | Normal | Detailed | Diag

    // Sln (msbuild/xbuild) task settings
    type MSBuildSettingsType = {
        // Build file location
        BuildFile: string

        Target: string list
        Property: Map<string,string>
        MaxCpuCount: int option

        ToolsVersion: string option
        Verbosity: SlnVerbosity option

        DetailedSummary: bool

        RspFile: string option

        // Build fails on compile error.
        FailOnError: bool
    }

[<AutoOpen>]
module DotnetTasks =

    open Common.impl

    /// Default setting for CSC task so that you could only override required settings
    let CscSettings = {
        Platform = AnyCpu
        Target = Auto    // try to resolve the type from name etc
        Out = Artifact.Undefined
        Src = Fileset.Empty
        Ref = Fileset.Empty
        RefGlobal = []
        Resources = []
        Define = []
        Unsafe = false
        CommandArgs = []
        FailOnError = true
    }

    module internal Impl =

        let private refIid = ref 0

        let internal newProcPrefix () = 
            System.Threading.Interlocked.Increment(refIid) |> sprintf "[CSC%i]"

        let tryLocateFwk name : option<FrameworkInfo> =
            let fwkRegKey = function
                //            | "1.1" -> "v1.1.4322"
                | "net-20" | "2.0" -> "v2.0.50727"
                | "net-30" | "3.0" -> "v3.0\Setup\InstallSuccess"
                | "net-35" | "3.5" -> "v3.5"
                | "net-40c" | "4.0-client" -> "v4\\Client"
                | "net-40" | "4.0"| "4.0-full" -> "v4\\Full"
                | _ -> failwithf "Unknown or unsupported profile '%s'" name

            let ndp = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP")
            if ndp = null then None
            else
                let fwk = ndp.OpenSubKey(fwkRegKey name)
                if fwk = null || not (fwk.GetValue("Install").Equals(1)) then
                    None
                else
                    Some {InstallPath = fwk.GetValue("InstallPath") :?> string; Version = fwk.GetValue("Version") :?> string}

        // attempts to locate framework, fails if not found
        let locateFwk name =
            match tryLocateFwk name with
            | Some i -> i
            | _ -> failwithf ".NET framework '%s' not found" name

        // attempts to locate any framework
        let locateFwkAny minimal =
            let flip f x y = f y x
            let applicableFrameworks =
                ["2.0"; "3.0"; "3.5"; "4.0"]
                |>  match minimal with
                    | Some m -> List.filter (flip (>=) m)
                    | _ -> List.rev     // in case minimal is not specified use the maximum available
            if isRunningOnMono then
                {InstallPath = ""; Version = "???"}
                // TODO detect mono version/path
            else
                match applicableFrameworks |> List.tryPick tryLocateFwk with
                    | Some i -> i
                    | _ -> failwith "No framework found"

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

        let compileResx (resxfile:FileInfo) (rcfile:FileInfo) =
            use resxreader = new System.Resources.ResXResourceReader (resxfile.FullName)
            resxreader.BasePath <- resxfile.DirectoryName

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
                let mapFile (file:FileInfo) =
                    let resname = makeResourceName o fo.BaseDir file.FullName in
                    (resname,file)

                let (Filelist l) = Fileset (fo,fs) |> (toFileList pathRoot) in
                l |> List.map mapFile

        let compileResxFiles = function
            | (res,(file:FileInfo)) when file.Extension.Equals(".resx", System.StringComparison.OrdinalIgnoreCase) ->
                let tempfile = new System.IO.FileInfo (Path.GetTempFileName())
                do compileResx file tempfile
                (Path.ChangeExtension(res,".resources"),tempfile,true)
            | (res,file) ->
                (res,file,false)
    // end of Impl module

    /// C# compiler task
    let Csc settings =

        let outFile = settings.Out

        let resolveTarget (name:string) =
            if name.EndsWith (".dll", System.StringComparison.OrdinalIgnoreCase) then Library else
            if name.EndsWith (".exe", System.StringComparison.OrdinalIgnoreCase) then Exe else
            Library

        let rec targetStr = function
            |AppContainerExe -> "appcontainerexe" |Exe -> "exe" |Library -> "library" |Module -> "module" |WinExe -> "winexe" |WinmdObj -> "winmdobj"
            |Auto -> outFile.Name |> resolveTarget |> targetStr
        let platformStr = function
            |AnyCpu -> "anycpu" |AnyCpu32Preferred -> "anycpu32preferred" |ARM -> "arm" | X64 -> "x64" | X86 -> "x86" |Itanium -> "itanium"
        
        let pfx = Impl.newProcPrefix()

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

            let args =
                seq {
                    yield "/nologo"

                    yield "/target:" + targetStr settings.Target
                    yield "/platform:" + platformStr settings.Platform

                    if settings.Unsafe then
                        yield "/unsafe"

                    if not outFile.IsUndefined then
                        yield sprintf "/out:%s" outFile.FullName

                    if not (List.isEmpty settings.Define) then
                        yield "/define:" + (settings.Define |> String.concat ";")

                    yield! src |> List.map (fun f -> f.FullName) 
                    yield! refs |> List.map ((fun f -> f.FullName) >> (+) "/r:")
                    yield! settings.RefGlobal |> List.map ((+) "/r:")

                    yield! resinfos |> List.map (fun(name,file,_) -> sprintf "/res:%s,%s" file.FullName name)
                    yield! settings.CommandArgs
                }

            let! dotnetFwk = getVar "NETFX"
            let fwkInfo = Impl.locateFwkAny dotnetFwk
            let cscPath =
                if isRunningOnMono then
                    // TODO detect mono is installed and die earlier, handle target framework
                    "mcs"
                else
                    let csc_exe = Path.Combine(fwkInfo.InstallPath, "csc.exe") in
                    csc_exe

// for short args this is ok, otherwise use rsp file --    let commandLine = args |> escapeAndJoinArgs
            let rspFile = Path.GetTempFileName()
            File.WriteAllLines(rspFile, args |> Seq.map Impl.escapeArgument |> List.ofSeq)
            let commandLine = "@" + rspFile

            do! writeLog Info "%s compiling '%s' using framework '%s'" pfx outFile.Name fwkInfo.Version
            do! writeLog Debug "Command line: '%s'" (args |> Seq.map Impl.escapeArgument |> String.concat "\r\n\t")

            let options = {
                SystemOptions with
                    LogPrefix = pfx
                    StdOutLevel = Level.Verbose     // consider standard compiler output too noisy
                }
            let! exitCode = _system options cscPath commandLine

            do! writeLog Level.Verbose "Deleting temporary files"
            seq {
                yield rspFile

                yield! query {
                    for (_,file,istemp) in resinfos do
                        where istemp
                        select file.FullName
                }
            }
            |> Seq.iter File.Delete

            do! writeLog Info "%s done '%s'" pfx outFile.Name
            if exitCode <> 0 then
                do! writeLog Error "%s ('%s') failed with exit code '%i'" pfx outFile.Name exitCode
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" pfx
        }

    (* csc options builder *)
    type CscSettingsBuilder() =

        [<CustomOperation("target")>]   member this.Target(s, value) =      {s with Target = value}
        [<CustomOperation("out")>]      member this.OutFile(s, value) =     {s with Out = value}
        [<CustomOperation("src")>]      member this.SrcFiles(s, value) =    {s with Src = value}

        [<CustomOperation("ref")>]     member this.Ref(s, value) =         {s with Ref = s.Ref + value}
        [<CustomOperation("refif")>]     member this.Refif(s, cond, (value:Fileset)) =         {s with Ref = s.Ref +? (cond,value)}

        [<CustomOperation("refs")>]     member this.Refs(s, value) =         {s with Ref = value}
        [<CustomOperation("grefs")>]    member this.RefGlobal(s, value) =   {s with RefGlobal = value}
        [<CustomOperation("resources")>] member this.Resources(s, value) =   {s with CscSettingsType.Resources = value :: s.Resources}
        [<CustomOperation("resourceslist")>] member this.ResourcesList(s, values) = {s with CscSettingsType.Resources = values @ s.Resources}

        [<CustomOperation("define")>]   member this.Define(s, value) =      {s with Define = value}
        [<CustomOperation("unsafe")>]   member this.Unsafe(s, value) =      {s with Unsafe = value}

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
        Property = Map.empty
        MaxCpuCount = None
        ToolsVersion = None
        Verbosity = None
        DetailedSummary = false
        RspFile = None
        FailOnError = true
    }

    let MSBuild (settings:MSBuildSettingsType) =

        action {
            let! dotnetFwk = getVar "NETFX"
            let fwkInfo = Impl.locateFwkAny dotnetFwk
            let buildAppPath =
                if isRunningOnMono then
                    // TODO detect mono is installed and die earlier, handle target framework
                    "xbuild"
                else
                    Path.Combine(fwkInfo.InstallPath, "msbuild.exe")

            let pfx = "msbuild" // TODO thread/index

            let options = {
                SystemOptions with
                    LogPrefix = pfx
                    StdOutLevel = Level.Info     // consider standard compiler output too noisy
                }

            let args =
                String.concat " " <|
                seq {
                    yield "/nologo"
                    yield settings.BuildFile

                    if not <| List.isEmpty settings.Target then
                        yield "/t:" + (settings.Target |> String.concat ";")

                    if Option.isSome settings.MaxCpuCount then yield sprintf "/m:%i" (Option.get settings.MaxCpuCount)
                    // TODO just "/m" (concurrent building)

                    if Option.isSome settings.ToolsVersion then yield sprintf "/toolsversion:%s" (Option.get settings.ToolsVersion)
                    if Option.isSome settings.RspFile then yield sprintf "@%s" (Option.get settings.RspFile)

                    // TODO verbosity
                    // TODO properties

                    if settings.DetailedSummary then yield "/ds"
                }

            do! writeLog Info "%s making '%s' using framework '%s'" pfx settings.BuildFile fwkInfo.Version
            do! writeLog Debug "Command line: '%s'" args

            let! exitCode = args |> _system options buildAppPath

            do! writeLog Info "%s done '%s'" pfx settings.BuildFile
            if exitCode <> 0 then
                do! writeLog Error "%s ('%s') failed with exit code '%i'" pfx settings.BuildFile exitCode
                if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" pfx
            ()
        }
