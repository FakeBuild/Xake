module Xake.Progress

open Xake.Util
open Xake.Estimate

/// <summary>
/// A message to a progress reporter.
/// </summary>
type ProgressMessage =
    | Begin of System.TimeSpan
    | Progress of System.TimeSpan * int
    | End

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
    extern IntPtr FindWindow(string (*lpClassName*),string (*lpWindowName*))

    /// <summary>
    /// Creates a Windows 7 taskbar progress indicator
    /// </summary>
    let createTaskbarIndicator () =

        try
            let ti = createTaskbarInstance
            let handle = FindWindow (null, System.Console.Title)
            let title = System.Console.Title

            let fmtTs (ts:System.TimeSpan) =
                (if ts.TotalHours >= 1.0 then "h'h'\ mm'm'\ ss's'"
                else if ts.TotalMinutes >= 1.0 then "mm'm'\ ss's'"
                else "'0m 'ss's'")
                |> ts.ToString

            // try to change a title to ensure we will not fail later
            System.Console.Title <- title

            function
            | Begin ts ->
                System.Console.Title <- sprintf "%s - %s" (fmtTs ts) title
                ti.SetProgressState (handle, TaskbarStates.Normal)
                ()
            | Progress (ts,pct) ->
                System.Console.Title <- sprintf "%i%% %s - %s" pct (fmtTs ts) title
                ti.SetProgressValue(handle, uint64 pct, 100UL)
                ()
            | End ->
                System.Console.Title <- title
                ti.SetProgressState (handle, TaskbarStates.NoProgress)
                ()
        with _ ->
            ignore

/// <summary>
/// Interface for progress module.
/// </summary>
type ProgressReport<'TKey> when 'TKey: comparison =
    | TaskStart of 'TKey
    | TaskSuspend of 'TKey
    | TaskResume of 'TKey
    | TaskComplete of 'TKey
    | Refresh
    | Finish

/// <summary>
/// Creates "null" progress reporter.
/// </summary>
let emptyProgress () = 
    MailboxProcessor.Start(fun mbox -> 
        let rec loop () = 
            async {
                let! msg = mbox.Receive()
                match msg with
                | Finish -> return ()
                | _ ->      return! loop()
            }
        loop ())

/// <summary>
/// Creates windows taskbar progress reporter.
/// </summary>
/// <param name="getDurationDeps">gets the dependency duration in ms</param>
/// <param name="threadCount"></param>
/// <param name="goals"></param>
let openProgress getDurationDeps threadCount goals toConsole = 

    let progressBar = WindowsProgress.createTaskbarIndicator() |> Impl.ignoreFailures
    let machineState = {Cpu = BusyUntil 0<Ms> |> List.replicate threadCount; Tasks = Map.empty}

    let _,endTime = execMany machineState getDurationDeps goals

    let startTime = System.DateTime.Now
    progressBar <| Begin (System.TimeSpan.FromMilliseconds (float endTime))

    /// We track currently running tasks and subtract already passed time from task duration
    let getDuration2 runningTasks t =
        match runningTasks |> Map.tryFind t with
        | Some(runningTime,_) ->
            let originalDuration,deps = getDurationDeps t
            //do printf "\nestimate %A: %A\n" t timeToComplete
            in
            originalDuration - runningTime |> max 0<Ms>, deps
        | _ ->
            getDurationDeps t

    let reportProgress (state, runningTasks) =
        let timePassed = 1<Ms> * int (System.DateTime.Now - startTime).TotalMilliseconds in
        let _,leftTime = execMany state (getDuration2 runningTasks) goals
        //printf "progress %A to %A " timePassed endTime
        let percentDone = timePassed * 100 / (timePassed + leftTime) |> int
        let progressData = System.TimeSpan.FromMilliseconds (leftTime/1<Ms> |> float), percentDone
        do Progress progressData |> progressBar
        if toConsole then
            do WriteConsoleProgress progressData

    let updTime = ref System.DateTime.Now
    let advanceRunningTime rt =
        let now = System.DateTime.Now
        let increment = 1<Ms> * int (now - !updTime).TotalMilliseconds
        updTime := now
        rt |> Map.map (
            fun _ (cpu,isRunning) ->
                let ncpu = if isRunning then cpu + increment else cpu in
                (ncpu,isRunning)
                )

    let suspend target t (cpu,isRunning) = (cpu,isRunning && t <> target)
    let resume  target t (cpu,isRunning) = (cpu,isRunning || t = target)

    MailboxProcessor.Start(fun mbox -> 
        let rec loop (state,runningTasks) = 
            async {
                try
                    let! msg = mbox.Receive(1000)
                    let runningTasks = runningTasks |> advanceRunningTime

                    match msg with
                    | TaskStart target ->    return! loop (state, runningTasks |> Map.add target (0<Ms>, true))
                    | TaskSuspend target ->  return! loop (state, runningTasks |> Map.map (suspend target))
                    | TaskResume target ->   return! loop (state, runningTasks |> Map.map (resume target))
                    | Refresh _ ->
                        reportProgress (state, runningTasks)
                        return! loop (state,runningTasks)

                    | TaskComplete target ->
                        let newState = ({state with Tasks = state.Tasks |> Map.add target 0<Ms>}, runningTasks |> Map.remove target)
                        reportProgress newState
                        return! loop newState

                    | Finish -> 
                        ProgressMessage.End |> progressBar
                        return ()
                with _ ->
                    mbox.Post Refresh
                    return! loop (state,runningTasks)
            }
        loop (machineState, Map.empty))
