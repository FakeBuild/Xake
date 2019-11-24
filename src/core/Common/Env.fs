module Xake.Env

/// <summary>
/// Gets true if script is executed under Mono framework.
/// </summary>
let isRunningOnMono =
  System.Type.GetType ("Mono.Runtime")
  |> function | null -> false | _ -> true

/// <summary>
/// Gets true if running under Unix/OSX (well, linux too).
/// </summary>
let isUnix =
  match System.Environment.OSVersion.Platform with
  | System.PlatformID.MacOSX | System.PlatformID.Unix -> true
  | _ -> false

/// <summary>
/// Gets true when started under Windows.
/// </summary>
let isWindows = not isUnix
