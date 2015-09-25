namespace Xake

open System.Collections
open System.IO
open System.Resources
open Xake

module (* internal *) pkg_config =

    open Xake.CommonTasks.impl

    let private pkgcgf args =
        let outp = ref option<string>.None
        let dump s = outp := match !outp with | None -> Some s | _ as str -> str
        try
            _pexec dump dump "pkg-config" (args |> String.concat " ") [] |> ignore
            match !outp with | None -> "" | Some str -> str
        with _ ->
            ""

    /// Gets true if specified package exists
    let private pkgcgf_bool args =
        let dump (s : string) = ()
        try
            0 = _pexec dump dump "pkg-config" (args |> String.concat " ") []
        with _ ->
            false
    /// Gets true if specified package exists
    let exists package = pkgcgf_bool ["--exists"; package]

    /// Get the version of a package
    let get_mod_version package = pkgcgf ["--modversion"; package]

    /// Get the version of a package
    let get_variable package var = pkgcgf ["--variable=\"" + var + "\""; package]
    let is_atleast_version package version = pkgcgf_bool ["--atleast-version=\"" + version + "\""; package]
    let is_exact_version package version = pkgcgf_bool ["--exact-version=\"" + version + "\""; package]

module DotNetFwk =

    type FrameworkInfo = {
        Version: string
        InstallPath: string
        AssemblyDirs: string list
        ToolDir: string
        CscTool: string
        FscTool: string option
        MsbuildTool: string
        EnvVars: (string* string) list
        }

    let private (~%) = System.Environment.GetEnvironmentVariable

    module internal registry =

        open Microsoft.Win32
        let HKLM = Registry.LocalMachine

        let open_subkey (hive:RegistryKey) key  = match hive.OpenSubKey(key) with | null -> None | k -> Some k
        let get_value key (hive:RegistryKey)    = match hive.GetValue(key) with |null -> None | v -> Some v
        let get_value_str h k                   = get_value h k |> Option.bind (string >> Some)


    // a set of functions and structures to detect Mono framework location
    module internal monoFwkImpl =

        open Xake
        open registry

        let MonoProbeKeys = [@"SOFTWARE\Wow6432Node\Novell\Mono"; @"SOFTWARE\Novell\Mono"]

        let tryLocateFwk fwk : option<FrameworkInfo> * string =

            let (monover,sdkroot,libdir,configdir,err) =
                if pkg_config.exists "mono" then
                    let prefix = pkg_config.get_variable "mono" "prefix" in
                    let winpath (str:string) = str.Replace('/', Path.DirectorySeparatorChar)
                    (
                        pkg_config.get_mod_version "mono",
                        prefix |> winpath,
                        pkg_config.get_variable "mono" "libdir" |> winpath,
                        prefix |> winpath </> "etc",
                        null
                    )
                else
                    let key = MonoProbeKeys |> List.tryPick (open_subkey HKLM)
                    let monover = key |> Option.bind (registry.get_value_str "DefaultCLR")
                    let monokey = monover |> Option.bind (open_subkey (Option.get key))

                    match monover, monokey with
                    | Some monover, Some monokey ->
                        let gets key = monokey |> registry.get_value_str key |> Option.get in
                        (
                            monover,
                            gets "SdkInstallRoot",
                            gets "FrameworkAssemblyDirectory",
                            gets "MonoConfigDir",
                            null
                        )
                    | _ ->
                        ("", "", "", "", "Failed to obtain mono framework location from registry")
            match err with
            | null ->
                let csc_tool = if pkg_config.is_atleast_version "mono" "3.0" then "mcs" else "dmcs"
                let fw_lib_path = libdir </> "mono" </> libdir
                let fwkinfo libpath ver = Some {
                        InstallPath = sdkroot
                        AssemblyDirs = [fw_lib_path]
                        ToolDir = fw_lib_path
                        Version = ver
                        CscTool = csc_tool
                        FscTool = Some "fsharpc"
                        MsbuildTool = "xbuild"
                        EnvVars =["PATH", sdkroot </> "bin" + ";" + (%"PATH")]
                    }
                // ^^^^^^^ TODO proper tool (xbuild) lookup, this lib/mono/xxx contains only specific tools

                match fwk with
                | "mono-20" | "mono-2.0" | "2.0" -> fwkinfo "2.0" "2.0.50727", null
                | "mono-35" | "mono-3.5" | "3.5" -> fwkinfo "3.5" "2.0.50727", null
                | "mono-40" | "mono-4.0" | "4.0" -> fwkinfo "4.0" "4.0.30319", null
                | "mono-45" | "mono-4.5" | "4.5" -> fwkinfo "4.0" "4.5.50709", null
                | _ ->
                    None, sprintf "Unknown or unsupported profile '%s'" fwk
            | _ ->
                None, err

    module internal MsImpl =
        open registry

        let tryLocateFwk fwk =

            // TODO drop Wow node lookup
            let fscTool =
                registry.open_subkey registry.HKLM @"SOFTWARE\Wow6432Node\Microsoft\FSharp\3.0\Runtime\v4.0"
                |> Option.bind (registry.get_value_str "")
                |> Option.bind (fun p -> System.IO.Path.Combine (p, "fsc.exe") |> Some)

            let fwkKey = open_subkey HKLM @"SOFTWARE\Microsoft\.NETFramework"
            let installRoot_ = fwkKey |> Option.bind (get_value_str "InstallRoot")
            let installRoot = installRoot_ |> Option.get    // TODO gracefully fail

            let (version,fwkdir,asmpaths,vars,err) =
                match fwk with
                | "net-20" | "net-2.0" | "2.0" ->
                    ("2.0.50727", "v2.0.50727",
                        [
                            installRoot </> "v2.0.50727"
                        ], [], null)
                | "net-35" | "net-3.5" | "3.5" ->
                    ("3.5", "v3.5",
                        [
                            installRoot </> "v2.0.50727"
                            %"ProgramFiles" </> @"Reference Assemblies\Microsoft\Framework\v3.0"
                            %"ProgramFiles" </> @"Reference Assemblies\Microsoft\Framework\v3.5"
                        ],
                        [("COMPLUS_VERSION", "v2.0.50727")], null)
                | "net-40" | "net-4.0" | "4.0" | "4.0-full"
                | "net-45" | "net-4.5" | "4.5"| "4.5-full" ->
                    ("4.0", "v4.0.30319",
                        [
                            installRoot </> "v4.0.30319"
                            installRoot </> "v4.0.30319" </> "WPF"
                            %"ProgramFiles" </> @"\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0"
                        ], [("COMPLUS_VERSION", "v4.0.30319")],null)
                | _ ->
                    ("", "", [], [], "framework is not available on this PC")

            match err with
            | null ->
                let fwkdir = installRoot </> fwkdir in
                Some {
                    InstallPath = fwkdir; ToolDir = fwkdir
                    Version = version
                    AssemblyDirs = asmpaths
                    CscTool = fwkdir </> "csc.exe"
                    FscTool = fscTool
                    MsbuildTool = fwkdir </> "msbuild.exe"
                    EnvVars = vars
                }, null
            | _ ->
                None, err

    module internal impl =

        let locateFramework (fwk) : FrameworkInfo =
            let flip f x y = f y x
            let startsWith fragment (s: string option) =
                match s with
                | None | Some null -> false
                | Some str -> str.StartsWith fragment

            let tryLocate =
                match fwk |> startsWith "mono-", fwk |> startsWith "net-" with
                | true, _ -> monoFwkImpl.tryLocateFwk
                | _, true -> MsImpl.tryLocateFwk
                | _,_ -> if Env.isRunningOnMono then monoFwkImpl.tryLocateFwk else MsImpl.tryLocateFwk

            match fwk with
            | None ->
                match ["2.0"; "3.0"; "3.5"; "4.0"] |> List.rev |> List.tryPick (tryLocate >> fst) with
                | (Some i) -> i
                | _ -> failwith "No framework found"
            | Some name ->
                match name |> tryLocate with
                | None, err -> failwith err
                | Some f,_ -> f

    /// <summary>
    /// Attempts to locate either .NET or Mono framework.
    /// </summary>
    /// <param name="fwk"></param>
    let locateFramework = CommonLib.memoize impl.locateFramework

    /// <summary>
    /// Locates "global" assembly for specific framework
    /// </summary>
    /// <param name="fwk"></param>
    let locateAssembly fwkInfo =
        let lookupFile file =
            fwkInfo.AssemblyDirs
            |> List.tryPick (fun dir ->
                let fullName = dir </> file
                match File.Exists(dir </> file) with
                | true -> Some fullName | _ -> None
            )
            |> function | Some x -> x | None -> file
            
        CommonLib.memoize lookupFile