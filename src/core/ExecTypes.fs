﻿namespace Xake

open System.Threading
open Xake.WorkerPool

/// Script execution options
type ExecOptions = {
    /// Defines project root folder
    ProjectRoot : string
    /// Maximum number of rules processed simultaneously.
    Threads: int

    /// custom logger
    CustomLogger: ILogger

    /// Log file and verbosity level.
    FileLog: string
    FileLogLevel: Verbosity

    /// Console output verbosity level. Default is Warn
    ConLogLevel: Verbosity
    /// Overrides "want", i.e. target list
    Targets: string list

    /// Global script variables
    Vars: (string * string) list

    /// Defines whether `run` should throw exception if script fails.
    /// Default is false which means to exit process with non-zero code.
    ThrowOnError: bool

    /// Ignores command line swithes
    IgnoreCommandLine: bool

    /// Disable logo message
    Nologo: bool

    /// Database file
    DbFileName: string

    /// Do not execute rules, just display run stats
    DryRun: bool

    /// Dump dependencies only
    DumpDeps: bool

    /// Dump dependencies only
    Progress: bool
} with static member Default = {
        ProjectRoot = System.IO.Directory.GetCurrentDirectory()
        Threads = System.Environment.ProcessorCount
        ConLogLevel = Normal

        CustomLogger = CustomLogger (fun _ -> false) ignore
        FileLog = "build.log"
        FileLogLevel = Chatty
        Targets = []
        ThrowOnError = false
        Vars = List<string*string>.Empty
        IgnoreCommandLine = false
        Nologo = false
        DbFileName = ".xake"
        DryRun = false
        DumpDeps = false
        Progress = true
    }
end

type ExecStatus = | Succeed | Skipped | JustFile
type TaskPool = Agent<ExecMessage<ExecStatus>>

/// Script execution context
type ExecContext = {
    TaskPool: TaskPool
    Db: Agent<Storage.DatabaseApi>
    Throttler: SemaphoreSlim
    Options: ExecOptions
    Rules: Rules<ExecContext>
    Logger: ILogger
    RootLogger: ILogger
    Progress: Agent<Progress.ProgressReport>
    Targets: Target list
    RuleMatches: Map<string,string>
    Ordinal: int
    NeedRebuild: Target list -> bool
}

module internal Util =

    let private nullableToOption = function | null -> None | s -> Some s
    let getEnvVar = System.Environment.GetEnvironmentVariable >> nullableToOption

    let private valueByName variableName = function |name,value when name = variableName -> Some value | _ -> None
    let getVar (options: ExecOptions) name = options.Vars |> List.tryPick (valueByName name)
