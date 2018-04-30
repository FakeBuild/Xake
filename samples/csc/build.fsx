// xake build file

// #r @"..\..\bin\Xake.dll"
#r "../../core/bin/Debug/net46/Xake.dll"

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

// System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  rules [
    "clean" ..> rm {file "a*.exe"}
    "main" <== ([1..20] |> List.map (sprintf "a%i.exe"))
    "a.exe" ..> recipe {
      do! Csc {
        CscSettings with
          Src = !! "a.cs"
        }
      }
    "*.exe" ..> recipe {

      let! targetName = getTargetFullName()
      do! trace Level.Info "Building %s" targetName

      do! Csc {
        CscSettings with
          Src = ls "*.cs"
        }
    }
  ]
}
