// common tasks
namespace Xake

module SystemTasks =
    open Xake
    open Xake.Env

    open System.Diagnostics

    // internal implementation
    let internal _pexec handleStd handleErr cmd args (envvars:(string * string) list) =
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

    type Options = {
        LogPrefix:string;
        StdOutLevel: string -> Level; ErrOutLevel: string -> Level;
        EnvVars: (string * string) list

        /// Indicates command has to be executed under mono/.net runtime
        UseClr: bool
        FailOnErrorLevel: bool
    }
    with static member Default = {
            LogPrefix = ""; StdOutLevel = (fun _ -> Level.Info); ErrOutLevel = (fun _ -> Level.Error);
            EnvVars = []
            UseClr = false
            FailOnErrorLevel = false
        }

    /// <summary>
    /// Executes system command. E.g. '_system SystemOptions "dir" []'
    /// </summary>
    let _system settings cmd args =
      
      let isExt file ext = System.IO.Path.GetExtension(file).Equals(ext, System.StringComparison.OrdinalIgnoreCase)

      action {
        let! ctx = getCtx()
        let log = ctx.Logger.Log

        do! trace Level.Debug "[system] envvars: '%A'" settings.EnvVars
        do! trace Level.Debug "[system] args: '%A'" args

        let handleErr s = log (settings.ErrOutLevel s) "%s %s" settings.LogPrefix s
        let handleStd s = log (settings.StdOutLevel s) "%s %s" settings.LogPrefix s

        let cmd, args =
            if isWindows && not <| isExt cmd ".exe" then
                "cmd.exe", (sprintf "/c %s %s" cmd args)
            else if settings.UseClr && not isWindows then
                "mono", cmd + " " + args
            else
                cmd, args
        let errorlevel = _pexec handleStd handleErr cmd args settings.EnvVars
        if errorlevel <> 0 && settings.FailOnErrorLevel then failwith "System command resulted in non-zero errorlevel"
        return errorlevel
    }

    type OptionsFn = Options -> Options

    /// <summary>
    /// Executes external process and waits until it completes
    /// </summary>
    /// <param name="opts">Options setters</param>
    /// <param name="cmd">Command or executable name.</param>
    /// <param name="args">Command arguments.</param>
    let system (opts: OptionsFn) (cmd: string) (args: string seq) =
      action {
        do! trace Info "[shell.run] starting '%s'" cmd
        let! exitCode = _system (opts Options.Default) cmd (args |> String.concat " ")
        do! trace Info "[shell.run] completed '%s' exitcode: %d" cmd exitCode

        return exitCode
      }

    let useClr: OptionsFn = fun o -> {o with UseClr = true}
    let checkErrorLevel: OptionsFn = fun o -> {o with FailOnErrorLevel = true}

[<AutoOpen>]
module CommonTasks =
    /// <summary>
    /// Executes external process and waits until it completes
    /// </summary>
    /// <param name="cmd">Command or executable name.</param>
    /// <param name="args">Command arguments.</param>
    [<System.Obsolete("Use shell id... instead")>]
    let system cmd args = SystemTasks.system id cmd args
