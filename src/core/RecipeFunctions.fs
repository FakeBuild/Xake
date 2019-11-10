[<AutoOpen>]
module Xake.Recipe

open Xake

/// <summary>
/// Ignores action result in case task returns the value but you don't need it.
/// </summary>
/// <param name="act"></param>
let Ignore act = act |> RecipeAlgebra.ignoreF

/// <summary>
/// Translates the recipe result.
/// </summary>
let map f (rc: Recipe<_,_>) = recipe {
    let! r = rc
    return f r
}

/// Gets action context.
let getCtx()     = Recipe (fun c -> async {return (c,c)})

/// <summary>
/// Gets current task result.
/// </summary>
// let getResult()  = Recipe (fun (s,_) -> async {return (s,s)})

/// <summary>
/// Updates the build result
/// </summary>
/// <param name="s'"></param>
let setCtx ctx = Recipe (fun _ -> async {return (ctx,())})

/// <summary>
/// Consumes the task output and in case condition is met raises the error.
/// </summary>
/// <param name="cond"></param>
/// <param name="act"></param>
let FailWhen cond err (act: Recipe<_,_>) =
    recipe {
        let! b = act
        if cond b then failwith err
        return b
    }

/// <summary>
/// Supplemental for FailWhen to verify errorlevel set by system command.
/// </summary>
let Not0 = (<>) 0

/// <summary>
/// Error handler verifying result of system command.
/// </summary>
/// <param name="act"></param>
let CheckErrorLevel rc = rc |> FailWhen Not0 "system command returned a non-zero result"

/// <summary>
/// Wraps action so that exceptions occured while executing action are ignored.
/// </summary>
/// <param name="act"></param>
let WhenError handler (rc:Recipe<_,_>) = 
    recipe {
        try
            let! r = rc
            return r
        with e -> return handler e 
    }
