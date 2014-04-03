namespace Xake

open System.IO
open Xake

[<AutoOpen>]
module DotnetTasks =

  type FrameworkInfo = {Version: string; InstallPath: string}

  let tryLocateFwk name : option<FrameworkInfo> =
    let fwkRegKey =
      match name with
      //      | "1.1" -> "v1.1.4322"
      //      | "1.1" -> "v1.1.4322"
      //      | "2.0" -> "v2.0.50727"
      //      | "3.0" -> "v3.0\Setup\InstallSuccess 
      | "net-35" | "3.5" -> "v3.5"
      | "net-40c" | "4.0-client" -> "v4\\Client"
      | "net-40" | "4.0"| "4.0-full" -> "v4\\Full"
      | _ -> failwithf "Unknown or unsupported profile '%s'" name

    let ndp = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP")
    if ndp = null then None
    else
      let fwk = ndp.OpenSubKey(fwkRegKey)
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
  let locateFwkAny() =
    let ipath n =
      match tryLocateFwk n with
      | Some i -> i.InstallPath
      | None -> null
    
    match ["3.5"; "4.0"] |> List.fold (fun a p -> if a <> null then a else ipath p) null with
      | null -> failwith "No framework found"
      | fi -> fi
    
  // CSC task and related types
  type TargetType = |Auto |AppContainerExe |Exe |Library |Module |WinExe |WinmdObj
  type TargetPlatform = |AnyCpu |AnyCpu32Preferred |ARM | X64 | X86 |Itanium
  type CscSettingsType =
    {
      Platform: TargetPlatform
      Target: TargetType
      Out: Artifact
      Src: FilesetType
      Ref: FilesetType
      RefGlobal: string list
      Resources: FilesetType
      Define: string list
      Unsafe: bool
      FailOnError: bool
    }
  // see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx
  // defines, optimize, warn, debug, platform

  /// Default setting for CSC task so that you could only override required settings
  let CscSettings = {
    Platform = AnyCpu;
    Target = Auto;  // try to resolve the type from name etc
    Out = null;
    Src = Fileset.Empty;
    Ref = Fileset.Empty;
    RefGlobal = [];
    Resources = Fileset.Empty;
    Define = [];
    Unsafe = false;
    FailOnError = true
  }

  let private refIid = ref 0

  let internal newProcPrefix () = 
    System.Threading.Interlocked.Increment(refIid) |> sprintf "[CSC%i]"

  /// C# compiler task
  let Csc settings =

    let resolveTarget (name:string) =
      if name.EndsWith (".dll", System.StringComparison.OrdinalIgnoreCase) then "library" else
      if name.EndsWith (".exe", System.StringComparison.OrdinalIgnoreCase) then "exe" else
      "library"
    
    let targetStr = function |AppContainerExe -> "appcontainerexe" |Exe -> "exe" |Library -> "library" |Module -> "module" |WinExe -> "winexe" |WinmdObj -> "winmdobj"| Auto -> resolveTarget settings.Out.Name
    let platformStr = function |AnyCpu -> "anycpu" |AnyCpu32Preferred -> "anycpu32preferred" |ARM -> "arm" | X64 -> "x64" | X86 -> "x86" |Itanium -> "itanium"

    let pfx = newProcPrefix()

    async {
      let src = settings.Src |> getFiles
      let refs = settings.Ref |> getFiles
      let ress = settings.Resources |> getFiles
      do! need (FileList (src @ refs @ ress))

      let args =
        seq {
          yield "/nologo"

          yield "/target:" + targetStr settings.Target
          yield "/platform:" + platformStr settings.Platform

          if settings.Unsafe then
            yield "/unsafe"

          if settings.Out <> null then
            yield sprintf "/out:%s" settings.Out.FullName

          if List.exists (fun _ -> true) settings.Define then
            yield "/define:" + System.String.Join(";", Array.ofList settings.Define)

          yield! src |> List.map fullname
          yield! refs |> List.map (fullname >> (+) "/r:")
          yield! settings.RefGlobal |> List.map ((+) "/r:")
          // TODO resources
        }

      let csc_exe = Path.Combine(locateFwkAny(), "csc.exe")

// for short args this is ok
//      let commandLine = args |> escapeAndJoinArgs
      let rspFile = Path.GetTempFileName()
      File.WriteAllLines(rspFile, args |> Seq.map escapeArg |> List.ofSeq)
      let commandLine = "@" + rspFile

      do log Level.Info "%s compiling '%s'" pfx settings.Out.Name

      let options = {
        SystemOptions with
          LogPrefix = pfx
          StdOutLevel = Level.Verbose   // consider standard compiler output too noisy
        }
      let! exitCode = _system options csc_exe commandLine
      do File.Delete rspFile

      do log Level.Info "%s done '%s'" pfx settings.Out.Name
      if exitCode <> 0 then
        do log Level.Error "%s ('%s') failed with exit code '%i'" pfx settings.Out.Name exitCode
        if settings.FailOnError then failwithf "Exiting due to FailOnError set on '%s'" pfx
    }

  (* csc options builder *)
  type CscSettingsBuilder() =

    [<CustomOperation("target")>]   member this.Target(s, value) =       {s with Target = value}
    [<CustomOperation("out")>]      member this.OutFile(s, value) =      {s with Out = value}
    [<CustomOperation("src")>]      member this.SrcFiles(s, value) =     {s with Src = value}

    [<CustomOperation("refs")>]     member this.Ref(s, value) =   {s with Ref = value}
    [<CustomOperation("grefs")>]    member this.RefGlobal(s, value) = {s with RefGlobal = value}
    [<CustomOperation("res")>]      member this.Resources(s, value) =    {s with Resources = value}

    [<CustomOperation("define")>]   member this.Define(s, value) =       {s with Define = value}
    [<CustomOperation("unsafe")>]   member this.Unsafe(s, value) =       {s with Unsafe = value}

    member this.Bind(x, f) = f x
    member this.Yield(()) = CscSettings
    member this.For(x, f) = f x

    member this.Zero() = CscSettings
    member this.Run(s:CscSettingsType) = Csc s

  let csc = CscSettingsBuilder()
