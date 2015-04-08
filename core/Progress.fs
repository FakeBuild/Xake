module Xake.Progress

/// <summary>
/// A message to a progress reporter
/// </summary>
type ProgressMessage =
    | Begin of System.TimeSpan
    | Progress of System.TimeSpan * int
    | End

open Xake.WindowsTaskbar

/// <summary>
/// Creates a Windows 7 taskbar progress reporter
/// </summary>
let createWindowsTaskbarProgress () =

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
        
(*
let progress = Xake.Progress.createWindowsTaskbarProgress()

progress <| Xake.Progress.Begin (new System.TimeSpan(0,2,11))
Thread.Sleep 500
progress <| Xake.Progress.Progress (new System.TimeSpan(0,1,8), 30)
Thread.Sleep 500
progress <| Xake.Progress.Progress (new System.TimeSpan(0,1,8), 90)
progress <| Xake.Progress.End

*)
