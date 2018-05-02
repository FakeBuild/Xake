#r "paket:
nuget Xake ~> 1.0 prerelease //"
// #load "./.fake/build1.fsx/intellisense.fsx"

open Xake

do xake {ExecOptions.Default with ConLogLevel = Verbosity.Diag} {

    phony "main" (action {
        do! trace Message "=============== Sample output follows this line\n\n"
        for loglevel in [Level.Command; Level.Message; Level.Error; Level.Warning; Level.Debug; Level.Info; Level.Verbose] do
            do! trace loglevel "Sample text"
        do! trace Message "\n\n\tend of Sample output follows this line"
    })
}