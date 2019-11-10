namespace Xake

open System.Threading
open Prelude
open Xake.Database

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

    /// Defines whether `run` should throw exception if script fails
    FailOnError: bool

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
} with
static member Default = {
    ProjectRoot = System.IO.Directory.GetCurrentDirectory()
    Threads = System.Environment.ProcessorCount
    ConLogLevel = Normal

    CustomLogger = CustomLogger (fun _ -> false) ignore
    FileLog = "build.log"
    FileLogLevel = Chatty
    Targets = []
    FailOnError = false
    Vars = List<string*string>.Empty
    IgnoreCommandLine = false
    Nologo = false
    DbFileName = ".xake"
    DryRun = false
    DumpDeps = false
    Progress = true }
end

type ExecStatus = | Succeed | Skipped | JustFile
type TaskPool = Agent<WorkerPool.ExecMessage<ExecStatus>>

type BuildResult =
    { Targets : Target list
      Built : Timestamp
      Depends : Dependency list
      Steps : StepInfo list }

/// Script execution context
type ExecContext = {
    TaskPool: TaskPool
    Db: Agent<DatabaseApi<BuildResult>>
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
    Result: BuildResult
}

module internal Util =

    let private nullableToOption = function | null -> None | s -> Some s
    let getEnvVar = System.Environment.GetEnvironmentVariable >> nullableToOption

    let private valueByName variableName = function |name,value when name = variableName -> Some value | _ -> None
    let getVar (options: ExecOptions) name = options.Vars |> List.tryPick (valueByName name)

/// Utility methods to manipulate build stats   // TODO moveme
module internal Step =

    type DateTime = System.DateTime

    let start name = {StepInfo.Empty with Name = name; Start = DateTime.Now}

    /// <summary>
    /// Updated last (current) build step
    /// </summary>
    let updateLastStep fn = function
        | {Steps = current :: rest} as result -> {result with Steps = (fn current) :: rest}
        | result -> result

    /// <summary>
    /// Adds specific amount to a wait time
    /// </summary>
    let updateWaitTime delta = updateLastStep (fun c -> {c with WaitTime = c.WaitTime + delta})
    let updateTotalDuration =
        let durationSince (startTime: DateTime) = int (DateTime.Now - startTime).TotalMilliseconds * 1<ms>
        updateLastStep (fun c -> {c with OwnTime = (durationSince c.Start) - c.WaitTime})
    let lastStep = function
        | {Steps = current :: _} -> current
        | _ -> start "dummy"
    
module internal BuildResult =
    /// Creates a new build result
    let makeResult target = 
        { Targets = target
          Built = System.DateTime.Now
          Depends = []
          Steps = [] }
