namespace Xake

open Xake
open System.IO

module (* internal *) pkg_config =

    open SystemTasks

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
            0 = _pexec dump dump "pkg-config" (args |> String.concat " ") [] None
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
        FscTool: string option -> string option
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

        let tryLocateFwk fwk : option<FrameworkInfo> * string =

            let (sdkroot,libdir,err) =
                if pkg_config.exists "mono" then
                    let prefix = pkg_config.get_variable "mono" "prefix" in

                    let winpath (str:string) = str.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    (
                        prefix |> winpath,
                        pkg_config.get_variable "mono" "libdir" |> winpath,
                        null
                    )
                else if Env.isWindows then
                    let MonoProbeKeys = [@"SOFTWARE\Wow6432Node\Novell\Mono"; @"SOFTWARE\Novell\Mono"]
                    let Mono48ProbeKeys = [@"SOFTWARE\Wow6432Node\Mono"; @"SOFTWARE\Mono"]

                    MonoProbeKeys |> List.tryPick (open_subkey HKLM)
                    |>
                    function
                    | Some key ->
                        let monover = key |> registry.get_value_str "DefaultCLR"
                        monover |> Option.bind (open_subkey key)
                    | _ ->
                        Mono48ProbeKeys |> List.tryPick (open_subkey HKLM)
                    |>
                    function
                    | Some monokey ->
                        let gets key = monokey |> registry.get_value_str key |> Option.get in
                        (
                            gets "SdkInstallRoot",
                            gets "FrameworkAssemblyDirectory",
                            null
                        )
                    | _ ->
                        ("", "", "Failed to locate default mono version")
                else
                    ("", "", "Failed to obtain mono framework (check if mono and pkg_config are installed)")
            match err with
            | null ->
                let cscTool = if pkg_config.is_atleast_version "mono" "3.0" then "mcs" else "dmcs"

                let fwkinfo libpath ver =
                    let libPath = libdir </> "mono" </> libpath
                    let r = Some {
                        InstallPath = sdkroot
                        AssemblyDirs = [libPath]
                        ToolDir = libPath
                        Version = ver
                        CscTool = cscTool
                        FscTool = fun _ -> Some "fsharpc"
                        MsbuildTool = "xbuild"
                        EnvVars =["PATH", sdkroot </> "bin" + ";" + (%"PATH")]
                    }

                    r
                // ^^^^^^^ TODO proper tool (xbuild) lookup, this lib/mono/xxx contains only specific tools

                match fwk with
                | "mono-20" | "mono-2.0" | "2.0" -> fwkinfo "2.0" "2.0.50727", null
                | "mono-35" | "mono-3.5" | "3.5" -> fwkinfo "3.5" "2.0.50727", null
                | "mono-40" | "mono-4.0" | "4.0" -> fwkinfo "4.0" "4.0.30319", null
                | "mono-45" | "mono-4.5" | "4.5" -> fwkinfo "4.5" "4.5.50709", null
                | _ ->
                    None, sprintf "Unknown or unsupported profile '%s'" fwk
            | _ ->
                None, err

    module internal MsImpl =
        open registry

        let ifNone f arg = function
            | None -> f arg
            | x -> x
        let tryUntil (f: 't -> 'r option) (data: 't seq) : 'r option =
            data |> Seq.fold (fun state value -> state |> ifNone f value) None

        let tryLocateFwk fwk =

            // TODO drop Wow node lookup, lookup depending on fwk
            let fscTool ver =
                match ver with
                    | None -> ["4.1"; "4.0"; "3.1"; "3.0"]
                    | Some v -> [v]
                |> tryUntil (
                    sprintf @"SOFTWARE\Wow6432Node\Microsoft\FSharp\%s\Runtime\v4.0" >> registry.open_subkey registry.HKLM
                )
                |> Option.bind (registry.get_value_str "")
                |> Option.map (fun p -> p </> "fsc.exe")
 
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
    let locateFramework =
        CommonLib.memoize impl.locateFramework

    /// <summary>
    /// Locates "global" assembly for specific framework
    /// </summary>
    /// <param name="fwk"></param>
    let locateAssembly fwkInfo =
        let lookupFile file =
            fwkInfo.AssemblyDirs
            //["/Library/Frameworks/Mono.framework/Versions/Current/lib/pkgconfig/../../lib/mono/4.0"]
            |> List.tryPick (fun dir ->
                let fullName = dir </> file
                if File.Exists(fullName) then
                    Some fullName
                else
                    let fullName = fullName + ".dll"
                    if File.Exists(fullName) then
                        Some fullName
                    else
                        None
            )
            |> function | Some x -> x | None -> file
            
        CommonLib.memoize lookupFile