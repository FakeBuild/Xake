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

/// Updates the build result
let setCtx ctx = Recipe (fun _ -> async {return (ctx,())})

/// Consumes the task output and in case condition is met raises the error.
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
