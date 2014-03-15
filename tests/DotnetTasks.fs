namespace Xake

open System.IO
open Xake.Logging
open Xake.DomainTypes
open Xake.Core
open Xake.Common

[<AutoOpen>]
module Tasks =

  let private identity i = i

  // reads the file and returns all text
  let readtext artifact =
    artifact |> fullname |> File.ReadAllText

  // generic method to turn any fn to async task
  let wait fn artifact = async {
    do! need [artifact]
    return artifact |> fn
    }

  // executes the fn on all artifacts
  let allf fn aa = async {
    let! results = aa |> (List.map fn) |> List.toSeq |> Async.Parallel
    return results |> Array.toList
    }

  let all aa = allf identity aa

[<AutoOpen>]
module DotnetTasks =

  type TargetType = |Exe |Dll
  type CscSettingsType = {Target: TargetType; OutFile: FileInfo; SrcFiles: ArtifactType list; References: ArtifactType list}
  // see http://msdn.microsoft.com/en-us/library/78f4aasd.aspx
  // defines, optimize, warn, debug

  let CscSettings = {Target = Exe; OutFile = null; SrcFiles = []; References = []}

  // start csc compiler
  let Csc settings = 
    async {
      do! need (settings.SrcFiles @ settings.References)

      let files = List.map fullname settings.SrcFiles
      let refs = List.map fullname settings.References

      // TODO call compiler
      do logInfo "Compiling %s" settings.OutFile.FullName
    }

  open System.Diagnostics
  open Xake.Logging

  // executes external command and waits until it completes
  let system cmdline =
    async {
      let pinfo =
        ProcessStartInfo
          ("cmd.exe","/c "+ cmdline,
            UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true, RedirectStandardOutput = true)

      let proc = new Process(StartInfo = pinfo)
     
      proc.ErrorDataReceived.Add(fun  e -> if e.Data <> null then log Level.Error "%s" e.Data)
      proc.OutputDataReceived.Add(fun e -> if e.Data <> null then log Level.Info "%s" e.Data)

      do log Level.Info "Starting '%s'" cmdline
      do proc.Start() |> ignore

      do proc.BeginOutputReadLine()
      do proc.BeginErrorReadLine()

      proc.EnableRaisingEvents <- true
      let! _ = Async.AwaitEvent proc.Exited

      return proc.ExitCode
    }
