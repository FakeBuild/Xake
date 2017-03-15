namespace Xake

[<AutoOpen>]
module ScriptFuncs =

    open XakeScript

    /// <summary>
    /// Gets the script options.
    /// </summary>
    let getCtxOptions () = recipe {
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

    let private record d = recipe {
        let! result = getResult()
        do! setResult { result with Depends = d :: result.Depends }
    }

    /// <summary>
    /// Instructs Xake to rebuild the target even if dependencies are not changed.
    /// </summary>
    let alwaysRerun() = Dependency.AlwaysRerun |> record

    /// <summary>
    /// Gets the environment variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getEnv variableName =
        let value = Util.getEnvVar variableName
        action {
            do! Dependency.EnvVar (variableName,value) |> record
            return value
        }

    /// <summary>
    /// Gets the global (options) variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getVar variableName = recipe {
        let! ctx = getCtx()
        let value = Util.getVar ctx.Options variableName

        do! Dependency.Var (variableName,value) |> record
        return value
    }

    /// <summary>
    /// Gets the list of files matching specified fileset.
    /// </summary>
    /// <param name="fileset"></param>
    let getFiles fileset = recipe {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot
        do! Dependency.GetFiles (fileset,files) |> record

        return files
    }

    /// <summary>
    /// Gets current target file
    /// </summary>
    let getTargetFile() = recipe {
        let! ctx = getCtx()
        return ctx.Tgt
            |> function
            | Some (FileTarget file) -> file
            | _ -> failwith "getTargetFile is not available for phony actions"
    }

    /// <summary>
    /// Gets current target file name with path
    /// </summary>
    let getTargetFullName() = recipe {
        let! file = getTargetFile()
        return File.getFullName file
    }

    let getRuleMatches () = recipe {
        let! ctx = getCtx()
        return ctx.RuleMatches
    }

    /// <summary>
    /// Gets group (part of the name) by its name.
    /// </summary>
    let getRuleMatch key = action {
        let! groups = getRuleMatches()
        return groups |> Map.tryFind key |> function |Some v -> v | None -> ""
    }


    /// <summary>
    /// Writes a message to a log.
    /// </summary>
    let trace = ExecCore.traceLog

    [<System.Obsolete("Use trace instead")>]
    let writeLog = ExecCore.traceLog

    /// Defines a rule that demands specified targets
    /// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
    let (<==) name targets = PhonyRule (name, recipe {
        do! need targets
        do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
    })
    let (==>) = (<==)

    [<System.Obsolete("Use ..> operator and getTargetFile() instead")>]
    let ( *> ) pattern (fnRule : File -> Action<'ctx,unit>) = FileRule (pattern, action {
        let! file = getTargetFile()
        do! fnRule file
    })

    [<System.Obsolete("Use ..> operator and getTargetMatch() instead")>]
    type RuleActionArgs =
        RuleActionArgs of File * Map<string,string>
        with
        /// Gets the resulting file.
        member this.File = let (RuleActionArgs (file,_)) = this in file
        /// Gets the full name of resulting file.
        member this.FullName = let (RuleActionArgs (file,_)) = this in File.getFullName file

        /// Gets group (part of the name) by its name.
        member this.GetGroup(key) =
            let (RuleActionArgs (_,groups)) = this in
            groups |> Map.tryFind key |> function |Some v -> v | None -> ""

    /// Contains a methods for accessing RuleActionArgs members.
    [<System.Obsolete("Use ..> operator and getTargetMatch() instead")>]
    module RuleArgs =

        let getFile (args:RuleActionArgs) = args.File
        let getFullName (RuleActionArgs (file,_)) = File.getFullName file

        /// Gets all matched groups.
        let getGroups (RuleActionArgs (_,groups)) = groups

        /// Gets group (part of the name) by its name.
        let getGroup key (args:RuleActionArgs) = args.GetGroup key

    [<System.Obsolete("Use ..> operator and getTargetMatch() instead")>]
    let ( %> ) pattern (fnRule : RuleActionArgs -> Action<'ctx,unit>) = FileRule (pattern, recipe {
        let! file = getTargetFile()
        let! groups = getRuleMatches()

        let args = RuleActionArgs (file, groups)
        do! fnRule args
    })

