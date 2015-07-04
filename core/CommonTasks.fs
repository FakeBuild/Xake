// common tasks

[<AutoOpen>]
module Xake.CommonTasks

/// Gets true if running under Mono framework
let internal isRunningOnMono =
  System.Type.GetType ("Mono.Runtime") <> null

let internal isRunningOnUnix =
  match System.Environment.OSVersion.Platform with
  | System.PlatformID.MacOSX | System.PlatformID.Unix -> true
  | _ -> false

let internal isRunningOnWin32 = not isRunningOnUnix

module internal impl =

    open Xake
    open System.Diagnostics

    // internal implementation
    let _pexec handleStd handleErr cmd args (envvars:(string * string) list) =
        let pinfo =
          ProcessStartInfo
            (cmd, args,
              UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
              RedirectStandardError = true, RedirectStandardOutput = true)

        for name,value in envvars do            
            pinfo.EnvironmentVariables.[name] <- value

        let proc = new Process(StartInfo = pinfo)

        proc.ErrorDataReceived.Add(fun e -> if e.Data <> null then handleErr e.Data)
        proc.OutputDataReceived.Add(fun e -> if e.Data <> null then handleStd e.Data)

        do proc.Start() |> ignore

        do proc.BeginOutputReadLine()
        do proc.BeginErrorReadLine()

        // task might be completed by that time
        Async.RunSynchronously <|
        async {
            do! Async.Sleep 50
            if proc.HasExited then
                return proc.ExitCode
            else
                proc.EnableRaisingEvents <- true
                do! Async.AwaitEvent proc.Exited |> Async.Ignore
                return proc.ExitCode
        }

    type SystemOptionsType = {LogPrefix:string; StdOutLevel: Level; ErrOutLevel: Level; EnvVars: (string * string) list}
    let SystemOptions = {LogPrefix = ""; StdOutLevel = Level.Info; ErrOutLevel = Level.Error; EnvVars = []}

    /// <summary>
    /// Executes system command. E.g. '_system SystemOptions "dir" []'
    /// </summary>
    let _system settings cmd args =

      let isExt file ext = System.IO.Path.GetExtension(file).Equals(ext, System.StringComparison.OrdinalIgnoreCase)

      action {
        let! ctx = getCtx()
        let log = ctx.Logger.Log

        do! writeLog Level.Debug "[system] envvars: '%A'" settings.EnvVars
        do! writeLog Level.Debug "[system] args: '%A'" args

        let handleErr = log settings.ErrOutLevel "%s %s" settings.LogPrefix
        let handleStd = log settings.StdOutLevel  "%s %s" settings.LogPrefix

        return
            if isRunningOnWin32 && not <| isExt cmd ".exe" then
                _pexec handleStd handleErr "cmd.exe" ("/c " + cmd + " " + args) settings.EnvVars
            else
                _pexec handleStd handleErr cmd args settings.EnvVars
    }

open impl

/// <summary>
/// Executes external process and waits until it completes
/// </summary>
/// <param name="cmd">Command or executable name.</param>
/// <param name="args">Command arguments.</param>
let system cmd args =
  action {
    do! writeLog Info "[system] starting '%s'" cmd
    let! exitCode = _system SystemOptions cmd (args |> String.concat " ")
    do! writeLog Info "[system] сompleted '%s' exitcode: %d" cmd exitCode

    return exitCode
  }

// reads the file and returns all text
//let readtext artifact =
//  artifact |> getFullname |> System.IO.File.ReadAllText
