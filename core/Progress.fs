module Xake.Progress

/// <summary>
/// A message to a progress reporter
/// </summary>
type ProgressMessage =
    | Begin of System.TimeSpan
    | Progress of System.TimeSpan * int
    | End

module WindowsProgress =

    open Xake.WindowsTaskbar

    /// <summary>
    /// Creates a Windows 7 taskbar progress indicator
    /// </summary>
    let createTaskbarIndicator () =

        try
            let ti = createTaskbarInstance
            let handle = FindWindow (null, System.Console.Title)
            let title = System.Console.Title

            let fmt_ts (ts:System.TimeSpan) = ts.ToString("h'h'\ mm'm'\ ss's'")

            function
            | Begin ts ->
                System.Console.Title <- sprintf "{%i%%} %s - %s" 0 (fmt_ts ts) title
                ti.SetProgressState (handle, TaskbarStates.Normal)
                ()
            | Progress (ts,pct) ->
                System.Console.Title <- sprintf "{%i%%} %s - %s" pct (fmt_ts ts) title
                ti.SetProgressValue(handle, uint64 pct, 100UL)
                ()
            | End ->
                System.Console.Title <- title
                ti.SetProgressState (handle, TaskbarStates.NoProgress)
                ()
        with _ ->
            fun _ -> ()

/// Estimate the task execution times
module Estimate =

    type CpuState = | BusyUntil of int
    type MachineState<'T when 'T:comparison> =
        { Cpu: CpuState list; Tasks: Map<'T,int>}

    /// <summary>
    ///  "Executes" one task
    /// </summary>
    /// <param name="state">Initial "machine" state</param>
    /// <param name="getDurationDeps">Provide a task information to exec function</param>
    /// <param name="task">The task to execute</param>
    let rec exec state getDurationDeps task =
        // Gets the thread that will be freed after specific moment
        let nearest after =
            let ready (BusyUntil x) = if x <= after then 0 else x in
            List.minBy ready

        // Updates the first item matching the criteria and returns the updated value
        let rec updateFirst predicate upd = function
            | [] -> None,[]
            | c::list when predicate c ->
                let updated = upd c in
                Some updated, updated :: list
            | c::list ->
                let result,list = (updateFirst predicate upd list) in
                result, c::list
        
        match state.Tasks |> Map.tryFind task with
        | Some result -> state,result
        | None ->
            let duration,deps = task |> getDurationDeps
            let readyAt (BusyUntil x)= x

            let mstate, endTime = execMany state getDurationDeps deps
            let slot = mstate.Cpu |> nearest endTime
            let Some (BusyUntil result), newState =
                mstate.Cpu |> updateFirst ((=) slot) (readyAt >> max endTime >> (+) duration >> BusyUntil)
            {Cpu = newState; Tasks = mstate.Tasks |> Map.add task result}, result
//        |> fun r ->
//            printf "after %A" (task_name task)
//            printf "   %A\n\n" r
//            r

    /// Executes multiple targers simultaneously
    and execMany state getDurationDeps goals =
        let machineState,endTime =
            goals |> List.fold (
                fun (prevState,prevTime) t ->
                    let newState,time = exec prevState getDurationDeps t in
                    (newState, max time prevTime)
                ) (state,0)

        machineState, endTime


(*
let progress = Xake.Progress.createWindowsTaskbarProgress()

progress <| Xake.Progress.Begin (new System.TimeSpan(0,2,11))
Thread.Sleep 500
progress <| Xake.Progress.Progress (new System.TimeSpan(0,1,8), 30)
Thread.Sleep 500
progress <| Xake.Progress.Progress (new System.TimeSpan(0,1,8), 90)
progress <| Xake.Progress.End

*)

type ProgressReport =
    | TaskStart of Target
    | TaskComplete of Target
    | Finish

// TODO isolate implementation in private nested module

open Estimate

let emptyProgress () = 
    MailboxProcessor.Start(fun mbox -> 
        let rec loop () = 
            async { 
                let! msg = mbox.Receive()
                match msg with
                | TaskStart _
                | TaskComplete _ -> 
                    return! loop()
                | Finish -> 
                    return ()
            }
        loop ())

let openProgress getDurationDeps threadCount goals = 

    let progressBar = WindowsProgress.createTaskbarIndicator()
    let machine_state = {Cpu = BusyUntil 0 |> List.replicate threadCount; Tasks = Map.empty}

    let _,endTime = execMany machine_state getDurationDeps goals

    let startTime = System.DateTime.Now
    progressBar <| ProgressMessage.Begin (System.TimeSpan.FromSeconds (float endTime))

    //let db, dbwriter = impl.openDatabaseFile path logger
    MailboxProcessor.Start(fun mbox -> 
        let rec loop (state) = 
            async { 
                let! msg = mbox.Receive()
                match msg with
                | TaskStart _ -> 
                    return! loop state
                | TaskComplete target -> 

                    let timePassed = int (System.DateTime.Now - startTime).TotalSeconds in
                    let newState = {machine_state with Tasks = machine_state.Tasks |> Map.add target timePassed}

                    let _,endTime = execMany newState getDurationDeps goals
                    let percentDone = timePassed * 100 / endTime |> int
                    ProgressMessage.Progress (System.TimeSpan.FromSeconds (float endTime), percentDone)
                    |> progressBar

                    return! loop newState
                | Finish -> 
                    End |> progressBar
                    return ()
            }
        loop machine_state)
