module WindowsTaskbar

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
(*

example:
    let ti = createTaskbarInstance
    let handle = FindWindow (null, System.Console.Title)
    let title = System.Console.Title

    printfn "'%A''s window handle %A, taskbar %A" System.Console.Title handle ti
    ti.SetProgressState (handle, TaskbarStates.Normal)
    ti.SetProgressValue(handle, uint64 20, 100UL)
    System.Console.Title <- "{20%} " + title
    Thread.Sleep 400
    ti.SetProgressValue(handle, uint64 50, 100UL)
    System.Console.Title <- "{50%} " + title
    Thread.Sleep 400
    ti.SetProgressValue(handle, uint64 90, 100UL)
    System.Console.Title <- "{90%} " + title
    Thread.Sleep 400
    ti.SetProgressState (handle, TaskbarStates.NoProgress)
*)
