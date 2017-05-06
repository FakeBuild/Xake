// common tasks
namespace Xake

module SystemTasks =
    open Xake
    open Xake.Env

    open System.Diagnostics

    // internal implementation
    let internal _pexec handleStd handleErr cmd args (envvars:(string * string) list) workDir =
        let pinfo =
          ProcessStartInfo
            (cmd, args,
              UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
              RedirectStandardError = true, RedirectStandardOutput = true)

        for name,value in envvars do            
            pinfo.EnvironmentVariables.[name] <- value

        match workDir with
        | Some path -> pinfo.WorkingDirectory <- path
        | _ -> ()

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

    type ShellOptions = {
        Command: string
        Args: string seq
        LogPrefix:string
        StdOutLevel: string -> Level; ErrOutLevel: string -> Level
        EnvVars: (string * string) list
        WorkingDir: string option

        /// Indicates command has to be executed under mono/.net runtime
        UseClr: bool
        FailOnErrorLevel: bool
    }
    with static member Default = {
            Command = null; Args = []
            LogPrefix = ""; StdOutLevel = (fun _ -> Info); ErrOutLevel = (fun _ -> Error)
            EnvVars = []
            WorkingDir = None
            UseClr = false
            FailOnErrorLevel = false
        }

    /// <summary>
    /// Executes system command. E.g. '_system SystemOptions "dir" []'
    /// </summary>
    let _system settings =
      let cmd = settings.Command
      let args = (settings.Args |> String.concat " ")
      let isExt file ext = System.IO.Path.GetExtension(file).Equals(ext, System.StringComparison.OrdinalIgnoreCase)

      recipe {
        let! ctx = getCtx()
        let log = ctx.Logger.Log

        do! trace Level.Debug "[_system] settings: '%A'" settings

        let handleErr s = log (settings.ErrOutLevel s) "%s %s" settings.LogPrefix s
        let handleStd s = log (settings.StdOutLevel s) "%s %s" settings.LogPrefix s

        let cmd, args =
            if isWindows && not <| isExt cmd ".exe" then
                "cmd.exe", (sprintf "/c %s %s" cmd args)
            else if settings.UseClr && not isWindows then
                "mono", cmd + " " + args
            else
                cmd, args
        let errorlevel = _pexec handleStd handleErr cmd args settings.EnvVars settings.WorkingDir
        if errorlevel <> 0 && settings.FailOnErrorLevel then failwith "System command resulted in non-zero errorlevel"
        return errorlevel
    }

    let Shell (opts: ShellOptions) =
      recipe {
        let cmd = opts.Command
        do! trace Info "[shell] starting '%s'" cmd
        let! exitCode = _system opts
        do! trace Info "[shell] completed '%s' exitcode: %d" cmd exitCode

        return exitCode
      }

    type ExecOptionsFn = ShellOptions -> ShellOptions

    /// <summary>
    /// Executes external process and waits until it completes
    /// </summary>
    /// <param name="opts">Options setters</param>
    /// <param name="cmd">Command or executable name.</param>
    /// <param name="args">Command arguments.</param>
    [<System.Obsolete("Use Xake.SystemTasks.sys instead")>]
    let system (opts: ExecOptionsFn) (cmd: string) (args: string seq) =
        Shell ({ShellOptions.Default with Command = cmd; Args = args} |> opts)

    let useClr: ExecOptionsFn = fun o -> {o with UseClr = true}
    let checkErrorLevel: ExecOptionsFn = fun o -> {o with FailOnErrorLevel = true}
    let workingDir dir: ExecOptionsFn = fun o -> {o with WorkingDir = Some dir}

    type ShellBuilder() =

        [<CustomOperation("cmd")>]      member this.Command(a:ShellOptions, value) = {a with Command = value}
        [<CustomOperation("args")>]     member this.Args(a:ShellOptions, value) =    {a with Args = value}
        [<CustomOperation("workdir")>]  member this.WorkDir(a:ShellOptions, value) = {a with WorkingDir = Some value}
        [<CustomOperation("useclr")>]   member this.UseClr(a :ShellOptions) =        {a with UseClr = true}
        [<CustomOperation("failonerror")>] member this.FailOnError(a :ShellOptions)= {a with FailOnErrorLevel = true}

        member this.Bind(x, f) = f x
        member this.Yield(()) = ShellOptions.Default
        member x.For(sq, b) = for e in sq do b e

        member this.Zero() = ShellOptions.Default
        member this.Run(opts:ShellOptions) = Shell opts

    let shell = ShellBuilder()

[<AutoOpen>]
module CommonTasks =
    /// <summary>
    /// Executes external process and waits until it completes
    /// </summary>
    /// <param name="cmd">Command or executable name.</param>
    /// <param name="args">Command arguments.</param>
    [<System.Obsolete("Use Xake.SystemTasks.sys instead")>]
    let system cmd args = SystemTasks.system id cmd args
