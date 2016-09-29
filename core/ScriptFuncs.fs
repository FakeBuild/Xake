namespace Xake

[<AutoOpen>]
module ScriptFuncs =

    open XakeScript

    /// <summary>
    /// Gets the script options.
    /// </summary>
    let getCtxOptions () = action {
        let! (ctx: ExecContext) = getCtx()
        return ctx.Options
    }

    /// <summary>
    /// Executes and awaits specified artifacts.
    /// </summary>
    /// <param name="targets"></param>
    let need targets =
        action {
            let! ctx = getCtx()
            let t' = targets |> (List.map (ExecCore.makeTarget ctx))
            do! ExecCore.need t'
        }

    let needFiles (Filelist files) =
        files |> List.map FileTarget |> ExecCore.need

    /// <summary>
    /// Instructs Xake to rebuild the target even if dependencies are not changed.
    /// </summary>
    let alwaysRerun() = action {
        let! result = getResult()
        do! setResult { result with Depends = Dependency.AlwaysRerun :: result.Depends }
    }

    /// <summary>
    /// Gets the environment variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getEnv variableName =
        let value = Util.getEnvVar variableName
        action {
            let! result = getResult()
            do! setResult {result with Depends = Dependency.EnvVar (variableName,value) :: result.Depends}
            return value
        }

    /// <summary>
    /// Gets the global (options) variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getVar variableName = action {
        let! ctx = getCtx()
        let value = Util.getVar ctx.Options variableName

        // record the dependency
        let! result = getResult()
        do! setResult {result with Depends = Dependency.Var (variableName,value) :: result.Depends}

        return value
    }

    /// <summary>
    /// Gets the list of files matching specified fileset.
    /// </summary>
    /// <param name="fileset"></param>
    let getFiles fileset = action {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ [Dependency.GetFiles (fileset,files)]}

        return files
    }

    /// <summary>
    /// Writes a message to a log.
    /// </summary>
    let trace = ExecCore.traceLog

    [<System.Obsolete("Use trace instead")>]
    let writeLog = ExecCore.traceLog

    /// Defines a rule that demands specified targets
    /// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
    let (<==) name targets = PhonyRule (name,action {
        do! need targets
        do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
    })
    let (==>) = (<==)
