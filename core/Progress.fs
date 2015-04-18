module Xake.Progress

module internal WindowsProgress =

    open System
    open System.Runtime.InteropServices

    type TaskbarStates =
        | NoProgress    = 0
        | Indeterminate = 0x1
        | Normal        = 0x2
        | Error         = 0x4
        | Paused        = 0x8

    [<ComImportAttribute()>]
    [<GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")>]
    [<InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)>]
    type ITaskbarList3 =
    
        // ITaskbarList
        [<PreserveSig>] abstract HrInit : unit -> unit
        [<PreserveSig>] abstract AddTab : IntPtr -> unit
        [<PreserveSig>] abstract DeleteTab : IntPtr -> unit
        [<PreserveSig>] abstract ActivateTab : IntPtr -> unit
        [<PreserveSig>] abstract SetActiveAlt : IntPtr -> unit

        // ITaskbarList2
        // [<MarshalAs(UnmanagedType.Bool)>]
        [<PreserveSig>] abstract MarkFullscreenWindow : (IntPtr * bool) -> unit

        // ITaskbarList3
        [<PreserveSig>] abstract SetProgressValue : IntPtr * UInt64 * UInt64 -> unit
        [<PreserveSig>] abstract SetProgressState : IntPtr * TaskbarStates -> unit

    let createTaskbarInstance =
        let ty = System.Type.GetTypeFromCLSID (System.Guid "56FDF344-FD6D-11d0-958A-006097C9A090")
        Activator.CreateInstance ty :?> ITaskbarList3

    [<DllImport "user32.dll">] 
    extern IntPtr FindWindow(string lpClassName,string lpWindowName)

    /// <summary>
    /// Creates a Windows 7 taskbar progress indicator
    /// </summary>
    let createTaskbarIndicator () =

        try
            let ti = createTaskbarInstance
            let handle = FindWindow (null, System.Console.Title)
            let title = System.Console.Title

            let fmt_ts (ts:System.TimeSpan) =
                let format_string = function
                | _ when ts.TotalHours >= 1.0 -> "h'h'\ mm'm'\ ss's'"
                | _ when ts.TotalMinutes >= 1.0 -> "mm'm'\ ss's'"
                | _ -> "'0m 'ss's'"
                in
                format_string ts |> ts.ToString

            function
            | Begin ts ->
                System.Console.Title <- sprintf "%s - %s" (fmt_ts ts) title
                ti.SetProgressState (handle, TaskbarStates.Normal)
                ()
            | Progress (ts,pct) ->
                System.Console.Title <- sprintf "%i%% %s - %s" pct (fmt_ts ts) title
                ti.SetProgressValue(handle, uint64 pct, 100UL)
                ()
            | End ->
                System.Console.Title <- title
                ti.SetProgressState (handle, TaskbarStates.NoProgress)
                ()
        with _ ->
            fun _ -> ()

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

/// Estimate the task execution times
module Estimate =

    type CpuState = | BusyUntil of int
    type MachineState<'T when 'T:comparison> =
        { Cpu: CpuState list; Tasks: Map<'T,int>}

    /// <summary>
    /// "Executes" one task
    /// </summary>
    /// <param name="state">Initial "machine" state</param>
    /// <param name="getDurationDeps">Provide a task information to exec function</param>
    /// <param name="task">The task to execute</param>
    let rec exec state getDurationDeps task =
        // Gets the thread that will be freed after specific moment
        let nearest after =
            let ready (BusyUntil x) = if x <= after then 0 else x in
            List.minBy ready

        match state.Tasks |> Map.tryFind task with
        | Some result -> state,result
        | None ->
            let duration,deps = task |> getDurationDeps
            let readyAt (BusyUntil x)= x

            let mstate, endTime = execMany state getDurationDeps deps
            let slot = mstate.Cpu |> nearest endTime
            let Some (BusyUntil result), newState =
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
                ) (state,0)

        machineState, endTime

/// <summary>
/// Interface for progress module.
/// </summary>
type ProgressReport =
    | TaskStart of Target
    | TaskSuspend of Target
    | TaskResume of Target
    | TaskComplete of Target
    | Refresh
    | Finish

open Estimate

/// <summary>
/// Creates "null" progress reporter.
/// </summary>
let emptyProgress () = 
    MailboxProcessor.Start(fun mbox -> 
        let rec loop () = 
            async { 
                let! msg = mbox.Receive()
                match msg with
                | TaskStart _
                | TaskSuspend _
                | TaskResume _
                | Refresh _
                | TaskComplete _ -> 
                    return! loop()
                | Finish -> 
                    return ()
            }
        loop ())

/// <summary>
/// Creates windows taskbar progress reporter.
/// </summary>
/// <param name="getDurationDeps"></param>
/// <param name="threadCount"></param>
/// <param name="goals"></param>
let openProgress getDurationDeps threadCount goals = 

    let progressBar = WindowsProgress.createTaskbarIndicator()
    let machine_state = {Cpu = BusyUntil 0 |> List.replicate threadCount; Tasks = Map.empty}

    let _,endTime = execMany machine_state getDurationDeps goals

    let startTime = System.DateTime.Now
    progressBar <| ProgressMessage.Begin (System.TimeSpan.FromSeconds (float endTime))

    /// We track currently running tasks and subtract already passed time from task duration
    let getDuration2 running_tasks t =
        match running_tasks |> Map.tryFind t with
        | Some(running_time,_) ->
            let originalDuration,deps = getDurationDeps t
            //do printf "\nestimate %A: %A\n" t timeToComplete
            in
            originalDuration - running_time/1000 |> max 0, deps
        | _ ->
            getDurationDeps t

    let reportProgress (state,running_tasks) =
        let timePassed = int (System.DateTime.Now - startTime).TotalSeconds in
        let _,leftTime = execMany state (getDuration2 running_tasks) goals
        //printf "progress %A to %A " timePassed endTime
        let percentDone = timePassed * 100 / (timePassed + leftTime) |> int
        ProgressMessage.Progress (System.TimeSpan.FromSeconds (leftTime |> float), percentDone)
        |> progressBar

    let updTime = ref System.DateTime.Now
    let advanceRunningTime rt =
        let now = System.DateTime.Now
        let increment = int (now - !updTime).TotalMilliseconds
        updTime := now
        rt |> Map.map (
            fun t (cpu,is_running) ->
                let ncpu = if is_running then cpu + increment else cpu in
                (ncpu,is_running)
                )

    let suspend target t (cpu,is_running) = (cpu,is_running && t <> target)
    let resume  target t (cpu,is_running) = (cpu,is_running || t = target)

    MailboxProcessor.Start(fun mbox -> 
        let rec loop (state,running_tasks) = 
            async {
                try
                    let! msg = mbox.Receive(1000)
                    let running_tasks = running_tasks |> advanceRunningTime

                    match msg with
                    | TaskStart target ->    return! loop (state, running_tasks |> Map.add target (0,true))
                    | TaskSuspend target ->  return! loop (state, running_tasks |> Map.map (suspend target))
                    | TaskResume target ->   return! loop (state, running_tasks |> Map.map (resume target))
                    | Refresh _ ->
                        reportProgress (state,running_tasks)
                        return! loop (state,running_tasks)

                    | TaskComplete target ->
                        let newState = ({state with Tasks = state.Tasks |> Map.add target 0}, running_tasks |> Map.remove target)
                        reportProgress newState
                        return! loop newState

                    | Finish -> 
                        ProgressMessage.End |> progressBar
                        return ()
                with _ ->
                    mbox.Post Refresh
                    return! loop (state,running_tasks)
            }
        loop (machine_state, Map.empty))
