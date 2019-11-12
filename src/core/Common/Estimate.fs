module Xake.Estimate

open Xake.Util

[<Measure>] type Ms

type CpuState = | BusyUntil of int<Ms>
type MachineState<'T when 'T:comparison> =
    { Cpu: CpuState list; Tasks: Map<'T,int<Ms>>}

module internal Impl =
    /// <summary>
    /// Updates the first item matching the criteria and returns the updated value.
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="upd"></param>
    let rec updateFirst predicate upd = function
        | [] -> None,[]
        | c::list when predicate c ->
            let updated = upd c in
            Some updated, updated :: list
        | c::list ->
            let result,list = (updateFirst predicate upd list) in
            result, c::list

    let ignoreFailures f a =
        try
            f a
        with _ ->
            ()

/// <summary>
/// "Executes" one task
/// </summary>
/// <param name="state">Initial "machine" state</param>
/// <param name="getDurationDeps">Provide a task information to exec function</param>
/// <param name="task">The task to execute</param>
let rec exec state getDurationDeps task =
    // Gets the thread that will be freed after specific moment
    let nearest after =
        let ready (BusyUntil x) = if x <= after then 0<Ms> else x in
        List.minBy ready

    match state.Tasks |> Map.tryFind task with
    | Some result -> state,result
    | None ->
        let duration,deps = task |> getDurationDeps
        let readyAt (BusyUntil x)= x

        let mstate, endTime = execMany state getDurationDeps deps
        let slot = mstate.Cpu |> nearest endTime
        let (Some (BusyUntil result)|OtherwiseFail result), newState =
            mstate.Cpu |> Impl.updateFirst ((=) slot) (readyAt >> max endTime >> (+) duration >> BusyUntil)
        {Cpu = newState; Tasks = mstate.Tasks |> Map.add task result}, result
//        |> fun r ->
//            printf "after %A" task
//            printf "   %A\n\n" r
//            r

/// Executes multiple targers simultaneously
and execMany state getDurationDeps goals =
    let machineState,endTime =
        goals |> List.fold (
            fun (prevState,prevTime) t ->
                let newState,time = exec prevState getDurationDeps t in
                (newState, max time prevTime)
            ) (state, 0<Ms>)

    machineState, endTime

/// Gets estimated execution time for several target groups. Each group start when previous group is completed and runs in parallel.
let estimateEndTime getDurationDeps threadCount groups =
    let machineState = {Cpu = BusyUntil 0<Ms> |> List.replicate threadCount; Tasks = Map.empty}

    groups |> List.fold (fun (state, _) group -> 
        let newState, endTime = execMany state getDurationDeps group
        let newState = {newState with Cpu = newState.Cpu |> List.map (fun _ -> BusyUntil endTime)}
        newState, endTime
        ) (machineState, 0<Ms>)
    |> snd
