namespace Xake

[<AutoOpen>]
module DotnetTasks =

  open System.IO
  open Xake.DomainTypes
  open Xake.Logging

  type TargetType = |Exe |Dll
  type CscSettingsType = {Target: TargetType; OutFile: FileInfo; SrcFiles: ArtifactType list}
  let CscSettings = {Target = Exe; OutFile = null; SrcFiles = []}

  let Csc settings = 
    async {
      // TODO call compiler
      do logInfo "Compiling %s" settings.OutFile.FullName
    }
