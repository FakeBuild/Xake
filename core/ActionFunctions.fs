namespace Xake

[<AutoOpen>]
module Action =

    open Xake

    /// <summary>
    /// Ignores action result in case task returns the value but you don't need it.
    /// </summary>
    /// <param name="act"></param>
    let Ignore act = act |> A.ignoreF


    /// <summary>
    /// Gets action context.
    /// </summary>
    let getCtx()     = Action (fun (r,c) -> async {return (r,c)})

    /// <summary>
    /// Gets current task result.
    /// </summary>
    let getResult()  = Action (fun (s,_) -> async {return (s,s)})

    /// <summary>
    /// Gets current target file
    /// </summary>
    let getTargetFile() = action {
        let! result = getResult()
        return result.Result
            |> function
            | FileTarget file -> file
            | _ -> failwith "getTargetFile is not available for phony actions"
    }

    /// <summary>
    /// Updates the build result
    /// </summary>
    /// <param name="s'"></param>
    let setResult s' = Action (fun (_,_) -> async {return (s',())})

    /// <summary>
    /// Finalizes current build step and starts a new one
    /// </summary>
    /// <param name="name">New step name</param>
    let newstep name =
        Action (fun (r,_) ->
            async {
                let r' = Step.updateTotalDuration r
                let r'' = {r' with Steps = (Step.start name) :: r'.Steps}
                return (r'',())
            })
    
    /// <summary>
    /// Consumes the task output and in case condition is met raises the error.
    /// </summary>
    /// <param name="cond"></param>
    /// <param name="act"></param>
    let FailWhen cond err (act: Action<_,_>) =
        action {
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
    let CheckErrorLevel act = act |> FailWhen Not0 "system command returned a non-zero result"

    /// <summary>
    /// Wraps action so that exceptions occured while executing action are ignored.
    /// </summary>
    /// <param name="act"></param>
    let WhenError handler (act:Action<_,_>) = 
        action {
            try
                let! r = act
                return r
            with e -> return handler e 
        }