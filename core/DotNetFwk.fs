namespace Xake

open System.Collections
open System.IO
open System.Resources
open Xake

module (* internal *) pkg_config =

    open Xake.Common.impl

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
        ToolDir: string
        CscTool: string
        MsbuildTool: string
        EnvVars: (string* string) list
        }

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

        let private (~%) = System.Environment.GetEnvironmentVariable
        let MonoProbeKeys = [@"SOFTWARE\Wow6432Node\Novell\Mono"; @"SOFTWARE\Novell\Mono"]

        let tryLocateFwk fwk : option<FrameworkInfo> * string =

            let prefix =
                if pkg_config.exists "mono" then
                    Some <| pkg_config.get_variable "mono" "prefix"
                else
                    match MonoProbeKeys |> List.tryPick (open_subkey HKLM) with
                    | Some key ->
                        try
                            key
                                |> registry.get_value_str "DefaultCLR"
                                |> Option.bind (open_subkey key)
                                |> Option.bind (registry.get_value_str "SdkInstallRoot")
                        with _ ->
                            failwith "Failed to obtain mono framework location from registry"
                    | None -> None
            match prefix with
            | None -> None, "Mono was not found (pkg-config codepath)"
            | Some prefix ->
                let binpath = prefix.Replace('/', Path.DirectorySeparatorChar) </> "bin"
                let defaultMonoFwkInfo = {
                        InstallPath = prefix
                        ToolDir = ""
                        Version = "2.0.50727"
                        CscTool = if pkg_config.is_atleast_version "mono" "3.0" then "mcs" else "dmcs"
                        MsbuildTool = "xbuild"
                        EnvVars =["PATH", binpath + ";" + (%"PATH")]
                    }
                // TODO proper tool (xbuild) lookup
                match fwk with
                | "mono-20" | "mono-2.0" | "2.0" ->
                    Some {defaultMonoFwkInfo with ToolDir = prefix </> "lib/mono/2.0"}, null
                | "mono-35" | "mono-3.5" | "3.5" ->
                    Some {defaultMonoFwkInfo with ToolDir = prefix </> "lib/mono/3.5"}, null
                | "mono-40" | "mono-4.0" | "4.0" ->
                    Some {defaultMonoFwkInfo with ToolDir = prefix </> "lib/mono/4.0"; Version = "4.0.30319"}, null
                | "mono-45" | "mono-4.5" | "4.5" ->
                    Some {defaultMonoFwkInfo with ToolDir = prefix </> "lib/mono/4.5"; Version = "4.5.50709"}, null
                | _ ->
                    None, sprintf "Unknown or unsupported profile '%s'" fwk
                

    module internal MsImpl =

        open registry

        let getRegKey = function
            //            | "1.1" -> "v1.1.4322"
            | "net-20" | "net-2.0" | "2.0" -> "v2.0.50727"
            | "net-30" | "net-3.0" | "3.0" -> "v3.0\Setup\InstallSuccess"
            | "net-35" | "net-3.5" | "3.5" -> "v3.5"
            | "net-40c"| "net-4.0c" | "4.0-client"
                -> "v4\\Client"
            | "net-40" | "net-4.0" | "4.0"| "4.0-full"
            | "net-45" | "net-4.5" | "4.5"| "4.5-full"
                -> "v4\\Full"
            | _ -> null

        let tryLocateFwk fwk : FrameworkInfo option * string =

            match getRegKey fwk, open_subkey HKLM @"SOFTWARE\Microsoft\NET Framework Setup\NDP" with
            | null, _ -> None, sprintf "unknown or unsupported profile '%s'" fwk
            | _, None -> None, "cannot open/find .NET Framework Setup registry key"
            | key, (Some ndp) ->
                let fwk_ = open_subkey ndp key
                match fwk_, fwk_ |> Option.bind (get_value "Install") with
                | Some fwk, Some o when o.Equals(1) ->
                    let installPath = fwk |> get_value_str "InstallPath" |> Option.get
                    let version = fwk |> get_value_str "Version" |> Option.get

                    Some {
                        InstallPath = installPath; ToolDir = installPath; Version = version
                        CscTool = installPath </> "csc.exe"
                        MsbuildTool = installPath </> "msbuild.exe"
                        EnvVars = []
                    }, null
                | _ -> None, "framework is not available on this PC"

    module internal impl =

        type Key<'K> = K of 'K
        let memoize f =
            let cache = ref Map.empty
            fun x ->
                match !cache |> Map.tryFind (K x) with
                | Some v -> v
                | None ->
                    let res = f x
                    cache := !cache |> Map.add (K x) res
                    res

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
                | _,_ -> match isRunningOnMono with | true -> monoFwkImpl.tryLocateFwk | false -> MsImpl.tryLocateFwk

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
    let locateFramework = impl.memoize impl.locateFramework
