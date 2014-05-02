// common tasks

[<AutoOpen>]
module Xake.Common

open Xake
open System.IO
open System.Diagnostics

type internal SystemOptionsType = {LogPrefix:string; StdOutLevel: Level; ErrOutLevel: Level}
let internal SystemOptions = {LogPrefix = ""; StdOutLevel = Level.Info; ErrOutLevel = Level.Error}

// internal implementation
let internal _system settings cmd args =
  async {
    let pinfo =
      ProcessStartInfo
        (cmd, args,
          UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
          RedirectStandardError = true, RedirectStandardOutput = true)

    let proc = new Process(StartInfo = pinfo)

    proc.ErrorDataReceived.Add(fun  e -> if e.Data <> null then log settings.ErrOutLevel "%s %s" settings.LogPrefix e.Data)
    proc.OutputDataReceived.Add(fun e -> if e.Data <> null then log settings.StdOutLevel  "%s %s" settings.LogPrefix e.Data)

    do proc.Start() |> ignore

    do proc.BeginOutputReadLine()
    do proc.BeginErrorReadLine()

    proc.EnableRaisingEvents <- true
    do! Async.AwaitEvent proc.Exited |> Async.Ignore

    return proc.ExitCode
  }

// joins and escapes strings
let internal joinArgs (args:#seq<string>) =
  (" ", args |> Array.ofSeq) |> System.String.Join

// executes command
let internal _cmd cmdline (args : string list) =
  action {
    let! exitCode = _system SystemOptions "cmd.exe" (joinArgs (["/c"; cmdline] @ args))
    return exitCode
  } 

// executes external process and waits until it completes
let system cmd args =
  action {
    do log Level.Info "[system] starting '%s'" cmd
    let! exitCode = _system SystemOptions cmd (joinArgs args)
    do log Level.Info "[system] сompleted '%s' exitcode: %d" cmd exitCode
    return exitCode
  }


// executes command
let cmd cmdline (args : string list) =
  action {
    do log Level.Info "[cmd] starting '%s'" cmdline
    let! exitCode = _cmd cmdline args
    do log Level.Info "[cmd] completed '%s' exitcode: %d" cmdline exitCode
    return exitCode
  } 

// reads the file and returns all text
let readtext artifact =
  artifact |> getFullname |> File.ReadAllText
