#r "paket:
source https://api.nuget.org/v3/index.json
source ../../core/bin/Debug
framework: netstandard2.0
nuget Xake.Core //"
#load "./.fake/build.fsx/intellisense.fsx"

open Xake

do xake {ExecOptions.Default with ConLogLevel = Verbosity.Diag} {

    phony "main" (action {
        do! trace Message "=============== Sample output follows this line\n\n"
        for loglevel in [Level.Command; Level.Message; Level.Error; Level.Warning; Level.Debug; Level.Info; Level.Verbose] do
            do! trace loglevel "Sample text"
        do! trace Message "\n\n\tend of Sample output follows this line"
    })
}
