namespace Xake

open System.IO
open Xake

[<AutoOpen>]
module DotnetTasks =

  type FrameworkInfo = {Version: string; InstallPath: string}

  let tryLocateFwk name : option<FrameworkInfo> =
    let fwkRegKey = match name with
//      | "1.1" -> "v1.1.4322"
//      | "1.1" -> "v1.1.4322"
//      | "2.0" -> "v2.0.50727"
//      | "3.0" -> "v3.0\Setup\InstallSuccess 
      | "net-35" | "3.5" -> "v3.5"
      | "net-40c" | "4.0-client" -> "v4\\Client"
      | "net-40" | "4.0"| "4.0-full" -> "v4\\Full"
      | _ -> failwith "Unknown or unsupported profile '%s'" name

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
    | _ -> failwith ".NET framework '%s' not found" name

  // attempts to locate any framework
  let locateFwkAny() =
    let ipath n = match tryLocateFwk n with
      | Some i -> i.InstallPath
      | None -> null
    
    match ["3.5"; "4.0"] |> List.fold (fun a p -> if a <> null then a else ipath p) null with
      | null -> failwith "No framework found"
      | fi -> fi
    
  // CSC task and related types
  type TargetType = |AppContainerExe |Exe |Library |Module |WinExe |WinmdObj
  type TargetPlatform = |AnyCpu |AnyCpu32Preferred |ARM | X64 | X86 |Itanium
  type CscSettingsType =
    {
      Platform: TargetPlatform;
      Target: TargetType;
      OutFile: FileInfo;
      SrcFiles: ArtifactType list;
      References: ArtifactType list;
      Resources: ArtifactType list
    }
  // see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx
  // defines, optimize, warn, debug, platform

  let CscSettings = {Platform = AnyCpu; Target = Exe; OutFile = null; SrcFiles = []; References = []; Resources = []}

  // start csc compiler
  let Csc settings = 
    async {
      do logInfo "CSC %s" settings.OutFile.FullName
      do! need (settings.SrcFiles @ settings.References)

      let files = List.map fullname settings.SrcFiles
      let refs = List.map fullname settings.References

      // TODO quote names and arguments
      let args =
        seq {
          yield "/nologo"
          if settings.OutFile <> null then
            yield sprintf "/out:%s" settings.OutFile.FullName
          yield! files
        }

      let commandLine = (" ",args) |> System.String.Join
      let csc_exe = Path.Combine(locateFwkAny(), "csc.exe")

      do! system csc_exe commandLine |> Async.Ignore

      do logInfo "Done compiling %s" settings.OutFile.FullName
    }
