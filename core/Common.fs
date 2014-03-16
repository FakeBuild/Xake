// common tasks

[<AutoOpen>]
module Xake.Common

open Xake
open System.IO
open System.Diagnostics

// executes external process and waits until it completes
let system cmd args =
  async {
    let pinfo =
      ProcessStartInfo
        (cmd, args,
          UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
          RedirectStandardError = true, RedirectStandardOutput = true)

    let proc = new Process(StartInfo = pinfo)
     
    proc.ErrorDataReceived.Add(fun  e -> if e.Data <> null then log Level.Error "%s" e.Data)
    proc.OutputDataReceived.Add(fun e -> if e.Data <> null then log Level.Info "%s" e.Data)

    do log Level.Info "Starting '%s'" cmd
    do proc.Start() |> ignore

    do proc.BeginOutputReadLine()
    do proc.BeginErrorReadLine()

    proc.EnableRaisingEvents <- true
    let! _ = Async.AwaitEvent proc.Exited

    return proc.ExitCode
  }

// executes command
let cmd cmdline = system "cmd.exe" ("/c "+ cmdline)

// reads the file and returns all text
let readtext artifact =
  artifact |> fullname |> File.ReadAllText
