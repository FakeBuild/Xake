[<AutoOpen>]
module Xake.ScriptFuncs

open Xake.Util

/// Gets the script options.
let getCtxOptions () = recipe {
    let! (ctx: ExecContext) = getCtx()
    return ctx.Options
}

/// Executes and awaits specified artifacts.
let need targets =
    recipe {
        let! ctx = getCtx()
        let t' = targets |> (List.map (ExecCore.makeTarget ctx))
        do! ExecCore.need t'
    }

let needFiles (Filelist files) =
    files |> List.map FileTarget |> ExecCore.need

let private record d = recipe {
    let! ctx = getCtx()
    do! setCtx { ctx with Result = { ctx.Result with Depends = d :: ctx.Result.Depends } }
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

let private takeFile (FileTarget file | OtherwiseFailErr "Expected only a file targets" file) = Some file

/// Gets current target file
let getTargetFile() = recipe {
    let! ctx = getCtx()
    return ctx.Targets |> List.choose takeFile |> List.head
}

/// Gets current target file
let getTargetFiles() = recipe {
    let! (ctx: ExecContext) = getCtx()
    return ctx.Targets |> List.choose takeFile
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

/// Gets group (part of the name) by its name.
let getRuleMatch key = action {
    let! groups = getRuleMatches()
    return groups |> Map.tryFind key |> function |Some v -> v | None -> ""
}

/// Writes a message to a log.
let trace = ExecCore.traceLog

/// Defines a rule that demands specified targets
/// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
let (<==) name targets = PhonyRule (name, recipe {
    do! need targets
    do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
})

/// Finalizes current build step and starts a new one       // TODO put it somewhere
let newstep name = recipe {
    let! c = getCtx()
    let r' = BuildResult.updateTotalDuration c.Result
    let r'' = {r' with Steps = (BuildResult.startStep name) :: r'.Steps}
    do! setCtx { c with Result = r''}
}
    
/// Defines a rule which demands the other targets to be sequentially built.
/// Unlike '<==' operator, this one waits the completion of one before issuing another rule.
let (<<<) name targets = PhonyRule (name, recipe {
    for t in targets do
        do! need [t]
    do! alwaysRerun()
})
