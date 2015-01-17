﻿namespace Xake

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
        AssemblyDir: string
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
                    match MonoProbeKeys |> List.tryPick (open_subkey HKLM) with
                    | Some key ->
                        match key |> registry.get_value_str "DefaultCLR" with
                        | Some monover ->
                            match open_subkey key monover with
                            | Some monokey ->
                                (
                                    monover,
                                    monokey |> registry.get_value_str "SdkInstallRoot" |> Option.get,
                                    monokey |> registry.get_value_str "FrameworkAssemblyDirectory" |> Option.get,
                                    monokey |> registry.get_value_str "MonoConfigDir" |> Option.get,
                                    null
                                )
                            | _ ->
                                ("", "", "", "", "Failed to obtain subkey for mono version " + monover)
                        | _ ->
                            ("", "", "", "", "Failed to obtain mono framework location from registry")
                    | None ->
                        ("", "", "", "", "Mono was not found (pkg-config codepath)")
            match err with
            | null ->
                let binpath = sdkroot </> "bin"
                let defaultMonoFwkInfo = {
                        InstallPath = sdkroot
                        AssemblyDir = libdir
                        ToolDir = ""
                        Version = "2.0.50727"
                        CscTool = if pkg_config.is_atleast_version "mono" "3.0" then "mcs" else "dmcs"
                        MsbuildTool = "xbuild"
                        EnvVars =["PATH", binpath + ";" + (%"PATH")]
                    }
                // TODO proper tool (xbuild) lookup
                match fwk with
                | "mono-20" | "mono-2.0" | "2.0" ->
                    Some {defaultMonoFwkInfo with ToolDir = libdir </> "mono" </> "2.0"}, null
                | "mono-35" | "mono-3.5" | "3.5" ->
                    Some {defaultMonoFwkInfo with ToolDir = libdir </> "mono" </> "3.5"}, null
                | "mono-40" | "mono-4.0" | "4.0" ->
                    Some {defaultMonoFwkInfo with ToolDir = libdir </> "mono" </> "4.0"; Version = "4.0.30319"}, null
                | "mono-45" | "mono-4.5" | "4.5" ->
                    Some {defaultMonoFwkInfo with ToolDir = libdir </> "mono" </> "4.5"; Version = "4.5.50709"}, null
                | _ ->
                    None, sprintf "Unknown or unsupported profile '%s'" fwk
            | _ ->
                None, err

    module internal MsImpl =
        open registry

        let tryLocateFwk fwk =
            let fwkKey = open_subkey HKLM @"SOFTWARE\Microsoft\.NETFramework"
            let installRoot_ = fwkKey |> Option.bind (get_value_str "InstallRoot")
            let installRoot = installRoot_ |> Option.get    // TODO gracefully fail

            // let sdkInstallRoot = fwkKey |> Option.bind (get_value_str "sdkInstallRoot") neither complete nor correct
            let (version,fwkdir,asmdir,vars,err) =
                match fwk with
                | "net-20" | "net-2.0" | "2.0" ->
                    let fwkdir = installRoot </> "v2.0.50727"
                    ("2.0.50727",fwkdir,fwkdir, [], null)
                | "net-35" | "net-3.5" | "3.5" ->
                    let fwkdir = installRoot </> "v3.5"
                    let asmdir = installRoot </> "v2.0.50727"
                    ("3.5",fwkdir,asmdir, [("COMPLUS_VERSION", "v2.0.50727")], null)
                | "net-40" | "net-4.0" | "4.0" | "4.0-full"
                | "net-45" | "net-4.5" | "4.5"| "4.5-full" ->
                    let fwkdir = installRoot </> "v4.0.30319"
                    ("4.0",fwkdir,fwkdir,[("COMPLUS_VERSION", "v4.0.30319")],null)
                | _ ->
                    ("", "", "", [], "framework is not available on this PC")

            match err with
            | null ->
                Some {
                    InstallPath = fwkdir; ToolDir = fwkdir; Version = version
                    AssemblyDir = asmdir
                    CscTool = fwkdir </> "csc.exe"
                    MsbuildTool = fwkdir </> "msbuild.exe"
                    EnvVars = vars
                }, null
            | _ ->
                None, err

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