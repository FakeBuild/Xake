namespace Xake.Tasks.Dotnet

open System.IO
open System.Resources
open Xake
open Xake.ProcessExec

[<AutoOpen>]
module DotNetTaskTypes =

    // CSC task and related types
    type TargetType = |Auto |AppContainerExe |Exe |Library |Module |WinExe |WinmdObj
    type TargetPlatform = |AnyCpu |AnyCpu32Preferred |ARM | X64 | X86 |Itanium
    // see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx
    // defines, optimize, warn, debug, platform

    type MsbVerbosity = | Quiet | Minimal | Normal | Detailed | Diag


module internal Impl =
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
        match true with
        | _ when isEmpty root ->
            path
        | _ when path.ToLowerInvariant().StartsWith (root.ToLowerInvariant()) ->
            path.Substring(root.Length).TrimStart('/', '\\')
        | _ -> path

    let endsWith e (str:string) = str.EndsWith (e, System.StringComparison.OrdinalIgnoreCase)
    let (|EndsWith|_|) e str = if endsWith e str then Some () else None

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
        if text.Contains "): warning " then Level.Warning
        else if text.Contains "): error " then Level.Error
        else defaultLevel
    let inline coalesce ls = //: 'a option list -> 'a option =
        ls |> List.fold (fun r a -> if Option.isSome r then r else a) None

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

    let collectResInfo pathRoot = function
        |ResourceFileset (o,Fileset (fo,fs)) ->
            let mapFile file =
                let resname = makeResourceName o fo.BaseDir (File.getFullName file) in
                (resname,file)

            let (Filelist l) = Fileset (fo,fs) |> (toFileList pathRoot) in
            l |> List.map mapFile

    let compileResx (resxfile:File) (rcfile:File) =
        use writer = new ResourceWriter (rcfile.FullName)

        // TODO here we have deal with types somehow because we are running conversion under framework 4.5 but target could be 2.0
        writer.TypeNameConverter <-
            fun(t:System.Type) ->
                t.AssemblyQualifiedName.Replace("4.0.0.0", "2.0.0.0")

#if NET46
        use resxreader = new System.Resources.ResXResourceReader (resxfile.FullName)
        resxreader.BasePath <- File.getDirName resxfile

        let reader = resxreader.GetEnumerator()
        while reader.MoveNext() do
            writer.AddResource (reader.Key :?> string, reader.Value)
#else
        failwith "ERROR: resx compilation is not supported under netstandard target"
#endif
        writer.Generate()

    let compileResxFiles = function
        | (res,(file:File)) when file |> File.getFileName |> endsWith ".resx" ->
            let tempfile = Path.GetTempFileName() |> File.make
            do compileResx file tempfile
            (Path.ChangeExtension(res,".resources"),tempfile,true)
        | (res,file) ->
            (res,file,false)

    /// <summary>
    /// Executes system command. E.g. '_system SystemOptions "dir" []'
    /// </summary>
    let _system cmd args envVars logPrefix =

        let StdOutLevel = fun _ -> Level.Verbose
        let ErrOutLevel = levelFromString Level.Verbose
        let argsStr = (args |> String.concat " ")

        recipe {
            let! ctx = getCtx()
            let log = ctx.Logger.Log

            // do! trace Level.Debug "[_system] settings: '%A'"

            let handleErr s = log (ErrOutLevel s) "%s %s" logPrefix s
            let handleStd s = log (StdOutLevel s) "%s %s" logPrefix s
            let workingDir = None

            return pexec handleStd handleErr cmd argsStr envVars workingDir
        }
