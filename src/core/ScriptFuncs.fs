namespace Xake

[<AutoOpen>]
module ScriptFuncs =

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
        recipe {
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
    let alwaysRerun() = AlwaysRerun |> record

    /// <summary>
    /// Gets the environment variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getEnv variableName =
        let value = Util.getEnvVar variableName
        recipe {
            do! EnvVar (variableName,value) |> record
            return value
        }

    /// <summary>
    /// Gets the global (options) variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getVar variableName = recipe {
        let! ctx = getCtx()
        let value = Util.getVar ctx.Options variableName

        do! Var (variableName,value) |> record
        return value
    }

    /// <summary>
    /// Gets the list of files matching specified fileset.
    /// </summary>
    /// <param name="fileset"></param>
    let getFiles fileset = recipe {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot
        do! GetFiles (fileset,files) |> record

        return files
    }

    /// <summary>
    /// Gets current target file
    /// </summary>
    let getTargetFile() = recipe {
        let! ctx = getCtx()
        return ctx.Targets
            |> function
            | FileTarget file::_ -> file
            | _ -> failwith "getTargetFile is not available for phony actions"
    }

    /// <summary>
    /// Gets current target file
    /// </summary>
    let getTargetFiles() : Recipe<ExecContext, File list> = recipe {
        let! ctx = getCtx()
        return ctx.Targets |> List.collect (function |FileTarget file -> [file] |_ -> failwith "Expected only a file targets"; [])
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
    let getRuleMatch key = recipe {
        let! groups = getRuleMatches()
        return groups |> Map.tryFind key |> function |Some v -> v | None -> ""
    }


    /// <summary>
    /// Writes a message to a log.
    /// </summary>
    let trace = ExecCore.traceLog

    /// Defines a rule that demands specified targets
    /// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
    let (<==) name targets = PhonyRule (name, recipe {
        do! need targets
        do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
    })

    let (<||) = (<==)
    
    /// Defines a rule which demands the other targets to be sequentially built.
    /// Unlike '<==' operator, this one waits the completion of one before issuing another rule.
    let (<<<) name targets = PhonyRule (name, recipe {
        for t in targets do
            do! need [t]
        do! alwaysRerun()
    })

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

