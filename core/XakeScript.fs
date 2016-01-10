namespace Xake

open Xake
    open System.Threading

[<AutoOpen>]
module XakeScript =

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
    } with
    static member Default =
        {
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
        }
    end

    type private ExecStatus = | Succeed | Skipped | JustFile
    type private TaskPool = Agent<WorkerPool.ExecMessage<ExecStatus>>

    type ExecContext = {
        TaskPool: TaskPool
        Db: Agent<Storage.DatabaseApi>
        Throttler: SemaphoreSlim
        Options: ExecOptions
        Rules: Rules<ExecContext>
        Logger: ILogger
        RootLogger: ILogger
        Progress: Agent<Progress.ProgressReport>
        Tgt: Target option
        Ordinal: int
        NeedRebuild: Target -> bool
    }

    /// Main type.
    type XakeScript = XakeScript of ExecOptions * Rules<ExecContext>

    /// <summary>
    /// Dependency state.
    /// </summary>
    type DepState =
        | NotChanged
        | Depends of Target * DepState list
        | Refs of string list
        | FilesChanged of string list
        | Other of string

    /// Default options
    [<System.Obsolete("Obsolete, use ExecOptions.Default")>]
    let XakeOptions = ExecOptions.Default

    module private Impl = begin
        open WorkerPool
        open Storage

        let nullableToOption = function | null -> None | s -> Some s
        let valueByName variableName = function |name,value when name = variableName -> Some value | _ -> None

        let TimeCompareToleranceMs = 100.0

        /// Writes the message with formatting to a log
        let traceLog (level:Logging.Level) fmt =
            let write s = action {
                let! ctx = getCtx()
                return ctx.Logger.Log level "%s" s
            }
            Printf.kprintf write fmt

        let addRule rule (Rules rules) :Rules<_> =    Rules (rule :: rules)

        let getEnvVar = System.Environment.GetEnvironmentVariable >> nullableToOption
        let getVar ctx name = ctx.Options.Vars |> List.tryPick (valueByName name)

        // Ordinal of the task being added to a task pool
        let refTaskOrdinal = ref 0

        // locates the rule
        let private locateRule (Rules rules) projectRoot target =
            let matchRule rule =
                match rule, target with

                |FileConditionRule (predicate,_), FileTarget file when file |> File.getFullName |> predicate ->
                    //writeLog Level.Debug "Found conditional pattern '%s'" name
                    // TODO let condition rule extracting named groups
                    Some (rule,[])

                |FileRule (pattern,_), FileTarget file ->
                    file
                    |> File.getFullName
                    |> Path.matches pattern projectRoot
                    |> Option.map (fun groups -> rule,groups)

                |PhonyRule (name,_), PhonyAction phony when phony = name ->
                    // writeLog Verbose "Found phony pattern '%s'" name
                    Some (rule, [])

                | _ -> None

            rules |> List.tryPick matchRule

        let private reportError ctx error details =
            do ctx.Logger.Log Error "Error '%s'. See build.log for details" error
            do ctx.Logger.Log Verbose "Error details are:\n%A\n\n" details

        let private raiseError ctx error details =
            do reportError ctx error details
            raise (XakeException(sprintf "Script failed (error code: %A)\n%A" error details))

        /// <summary>
        /// Creates a context for a new task
        /// </summary>
        let newTaskContext target ctx =
            let ordinal = System.Threading.Interlocked.Increment(refTaskOrdinal)
            let prefix = ordinal |> sprintf "%i> "
            in
            {ctx with Ordinal = ordinal; Logger = PrefixLogger prefix ctx.RootLogger; Tgt = Some target}

        /// <summary>
        /// Gets target execution time in the last run
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="target"></param>
        let getExecTime ctx target =
            (fun ch -> Storage.GetResult(target, ch)) |> ctx.Db.PostAndReply
            |> Option.map (fun r -> r.Steps |> List.sumBy (fun s -> s.OwnTime))
            |> function | Some t -> t | _ -> 0<ms>

        /// Gets single dependency state
        let getDepState getVar getFileList (isOutdatedTarget: Target -> DepState list) = function
            | FileDep (a:File, wrtime) when not((File.exists a) && abs((File.getLastWriteTime a - wrtime).TotalMilliseconds) < TimeCompareToleranceMs) ->
                let afile = a in
                DepState.FilesChanged [afile.Name]

            | ArtifactDep (FileTarget file) when not (File.exists file) ->
                DepState.Other <| sprintf "target doesn't exist '%s'" file.Name

            | ArtifactDep dependeeTarget ->
                let ls = dependeeTarget |> isOutdatedTarget
                in
                match ls |> List.exists ((<>) DepState.NotChanged) with
                | true -> DepState.Depends (dependeeTarget,ls)
                | false -> NotChanged

            | EnvVar (name,value) when value <> getEnvVar name ->
                DepState.Other <| sprintf "Environment variable %s was changed from '%A' to '%A'" name value (getEnvVar name)

            | Var (name,value) when value <> getVar name ->
                DepState.Other <| sprintf "Global script variable %s was changed '%A'->'%A'" name value (getVar name)

            | AlwaysRerun ->
                DepState.Other <| "alwaysRerun rule"

            | GetFiles (fileset,files) ->
                let newfiles = getFileList fileset
                let diff = compareFileList files newfiles

                if List.isEmpty diff then
                    NotChanged
                else
                    Other <| sprintf "File list is changed for fileset %A: %A" fileset diff
            | _ -> NotChanged

        /// <summary>
        /// Gets all changed dependencies
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="getTargetDeps">gets state for nested dependency</param>
        /// <param name="target">The target to analyze</param>
        let getDepsImpl ctx getTargetDeps target =

            let lastBuild = (fun ch -> GetResult(target, ch)) |> ctx.Db.PostAndReply
            let dep_state = getDepState (getVar ctx) (toFileList ctx.Options.ProjectRoot) getTargetDeps

            match lastBuild with
            | Some {BuildResult.Depends = []} ->
                [DepState.Other "No dependencies"]

            | Some {BuildResult.Result = FileTarget file} when not (File.exists file) ->
                [DepState.Other "target not found"]

            | Some {BuildResult.Depends = depends} ->
                let collapseFilesChanged =
                    ([], []) |> List.fold (fun (files, states) ->
                        function
                        | DepState.FilesChanged ls -> (ls @ files),states
                        | d -> files, d :: states
                        )
                    >> function | ([],states) -> states | (files,states) -> DepState.FilesChanged files :: states
                    >> List.rev

                depends |>
                    (List.map dep_state >> collapseFilesChanged >> List.filter ((<>) DepState.NotChanged))

            | _ ->
                [DepState.Other "Unknown state"]

        /// <summary>
        /// Gets dependencies for specific target in a human friendly form
        /// </summary>
        /// <param name="ctx"></param>
        let getPlainDeps getDepStates getExecTime ptgt =

            // strips the filelists to only 5 items
            let rec stripReasons = function
                | DepState.FilesChanged fileList -> fileList |> take 5 |> DepState.FilesChanged
                | DepState.Depends (t,deps) -> DepState.Depends (t, deps |> List.map stripReasons)
                | state -> state

            // make plain dependent targets list for specified target


            let rec visitTargets (dep,visited) =
                match dep with
                | DepState.Depends (t,deps) when visited |> Map.containsKey t ->
                    (DepState.Depends (t,[]), visited)

                | DepState.Depends (t,deps) ->
                    let deps',visited' =
                        List.fold (fun (deps,v) d ->
                            let d',v' = visitTargets (d,v) in
                            (d'::deps, v')
                        ) ([],visited |> Map.add t 1) deps

                    (DepState.Depends (t, deps' |> List.rev), visited')
                | s -> (s, visited)

            let stripDuplicates dep = visitTargets (dep, Map.empty) |>  fst

            // TODO where're phony actions
            // can I make it more inductive? Consider initial graph but with stripped targets

            let rec collectTargets = function
                | DepState.Depends (t,deps) -> t:: (deps |> List.collect collectTargets)
                | _ -> []

            //let ptgt t = t, t |> mg |> mergeDeps |> List.map stripReasons |> take 5
            // mg >> List.collect traverseTargets >> distinct >> List.map ptgt
            let resultList = ptgt |> getDepStates |> List.map stripDuplicates in

            let totalEstimate = resultList |> List.collect collectTargets |> distinct |> List.sumBy getExecTime
            printfn "Total execution estimate is %Ams" totalEstimate

            resultList |> List.collect collectTargets |> distinct |> List.map (fun t -> (t, getExecTime t))

            //resultList

        // executes single artifact
        let rec private execOne ctx target =

            let run action =
                async {
                    match ctx.NeedRebuild target with
                    | true ->
                        let taskContext = newTaskContext target ctx
                        do ctx.Logger.Log Command "Started %s as task %i" target.ShortName taskContext.Ordinal

                        do Progress.TaskStart target |> ctx.Progress.Post

                        let startResult = {BuildLog.makeResult target with Steps = [Step.start "all"]}
                        let! (result,_) = action (startResult,taskContext)
                        let result = Step.updateTotalDuration result

                        Store result |> ctx.Db.Post

                        do Progress.TaskComplete target |> ctx.Progress.Post
                        do ctx.Logger.Log Command "Completed %s in %A ms (wait %A ms)" target.ShortName (Step.lastStep result).OwnTime  (Step.lastStep result).WaitTime
                        return ExecStatus.Succeed
                    | false ->
                        do ctx.Logger.Log Command "Skipped %s (up to date)" target.ShortName
                        return ExecStatus.Skipped
                }

            let getAction groups = function
                | FileRule (_, action)
                | FileConditionRule (_, action) ->
                    let (FileTarget artifact) = target in
                    let (Action r) = action (RuleActionArgs (artifact,groups)) in
                    r
                | PhonyRule (_, Action r) -> r

            // result expression is...
            match target |> locateRule ctx.Rules ctx.Options.ProjectRoot with
            | Some(rule,groups) ->
                let groupsMap = groups |> Map.ofSeq
                let action = rule |> getAction groupsMap
                async {
                    let! waitTask = (fun channel -> Run(target, run action, channel)) |> ctx.TaskPool.PostAndAsyncReply
                    let! status = waitTask
                    return status, ArtifactDep target
                }
            | None ->
                target |> function
                | FileTarget file when File.exists file ->
                    async {return ExecStatus.JustFile, FileDep (file, File.getLastWriteTime file)}
                | _ -> raiseError ctx (sprintf "Neither rule nor file is found for '%s'" target.FullName) ""

        /// <summary>
        /// Executes several artifacts in parallel.
        /// </summary>
        and private execParallel ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

        /// <summary>
        /// Gets the status of dependency artifacts (obtained from 'need' calls).
        /// </summary>
        /// <returns>
        /// ExecStatus.Succeed,... in case at least one dependency was rebuilt
        /// </returns>
        and execNeed ctx targets : Async<ExecStatus * Dependency list> =
            async {
                ctx.Tgt |> Option.iter (Progress.TaskSuspend >> ctx.Progress.Post)

                do ctx.Throttler.Release() |> ignore
                let! statuses = targets |> execParallel ctx
                do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore

                ctx.Tgt |> Option.iter (Progress.TaskResume >> ctx.Progress.Post)

                let dependencies = statuses |> Array.map snd |> List.ofArray in

                return statuses
                |> Array.exists (fst >> (=) ExecStatus.Succeed)
                |> function
                    | true -> ExecStatus.Succeed,dependencies
                    | false -> ExecStatus.Skipped,dependencies
            }

        /// <summary>
        /// phony actions are detected by their name so if there's "clean" phony and file "clean" in `need` list if will choose first
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="name"></param>
        let makeTarget ctx name =
            let (Rules rr) = ctx.Rules
            if rr |> List.exists (function |PhonyRule (n,_) when n = name -> true | _ -> false) then
                PhonyAction name
            else
                ctx.Options.ProjectRoot </> name |> File.make |> FileTarget

        /// Executes the build script
        let run script =

            let (XakeScript (options,rules)) = script
            let logger = CombineLogger (ConsoleLogger options.ConLogLevel) options.CustomLogger

            let logger =
                match options.FileLog, options.FileLogLevel with
                | null,_ | "",_
                | _,Verbosity.Silent -> logger
                | logFileName,level -> CombineLogger logger (FileLogger logFileName level)

            let (throttler, pool) = WorkerPool.create logger options.Threads

            let start = System.DateTime.Now
            let db = Storage.openDb options.ProjectRoot logger

            let ctx = {
                Ordinal = 0
                TaskPool = pool; Throttler = throttler
                Options = options; Rules = rules
                Logger = logger; RootLogger = logger; Db = db
                Progress = Progress.emptyProgress()
                NeedRebuild = fun _ -> false
                Tgt = None
                }

            let runStep targetNames =
                // TODO wrap more elegantly
                let rec get_changed_deps = CommonLib.memoize (getDepsImpl ctx (fun x -> get_changed_deps x)) in

                let check_rebuild (target:Target) =
                    get_changed_deps >>
                    function
                    | [] -> false, ""
                    | DepState.Other reason::_            -> true, reason
                    | DepState.Depends (t,_) ::_          -> true, "Depends on target " + t.ShortName
                    | DepState.FilesChanged (file::_) ::_ -> true, "File(s) changed " + file
                    | reasons -> true, sprintf "Some reason %A" reasons
                    >>
                    function
                    | false, _ -> false
                    | true, reason ->
                        do ctx.Logger.Log Info "Rebuild %A: %s" target.ShortName reason
                        true
                    <| target

                let targets = targetNames |> List.map (makeTarget ctx)
                let getDurationDeps t =
                    let collectTargets = List.collect (function |Depends (t,_) -> [t] | _ -> [])
                    (getExecTime ctx t) / 1000<ms>, get_changed_deps t |> collectTargets
                let progressSink = Progress.openProgress getDurationDeps options.Threads targets

                let stepCtx = {ctx with NeedRebuild = check_rebuild; Progress = progressSink}

                try
                    targets |> execParallel stepCtx |> Async.RunSynchronously |> ignore
                finally
                    do Progress.Finish |> progressSink.Post

            logger.Log Info "Options: %A" options

            let rec unwindAggEx (e:System.Exception) = seq {
                match e with
                    | :? System.AggregateException as a -> yield! a.InnerExceptions |> Seq.collect unwindAggEx
                    | a -> yield a
                }

            // splits list of targets ["t1;t2"; "t3;t4"] into list of list.
            let targetLists =
                options.Targets
                |> function
                    | [] ->
                        do logger.Log Level.Message "No target(s) specified. Defaulting to 'main'"
                        [["main"]]
                    | tt ->
                        tt |> List.map (fun s -> s.Split(';', '|') |> List.ofArray)

            try
                try
                    for list in targetLists do
                        runStep list
                    logger.Log Message "\n\n\tBuild completed in %A\n" (System.DateTime.Now - start)
                with
                    | exn ->
                        let th = if options.FailOnError then raiseError else reportError
                        let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) in
                        th ctx (exn.Message + "\n" + (errors |> String.concat "\r\n            ")) exn
                        logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
                        // TODO exit(1)
            finally
                db.PostAndReply Storage.CloseWait

        /// <summary>
        /// "need" implementation
        /// </summary>
        let need targets =
            action {
                let startTime = System.DateTime.Now

                let! ctx = getCtx()
                let! _,deps = targets |> execNeed ctx

                let totalDuration = int (System.DateTime.Now - startTime).TotalMilliseconds * 1<ms>
                let! result = getResult()
                let result' = {result with Depends = result.Depends @ deps} |> (Step.updateWaitTime totalDuration)
                do! setResult result'
            }
    end

    /// Creates the rule for specified file pattern.
    let ( %> ) pattern fnRule = FileRule (pattern, fun targetArgs -> fnRule targetArgs)
    let ( *> ) pattern fnRule = FileRule (pattern, fun (RuleActionArgs (t,_)) -> fnRule t)
    let ( *?> ) fn fnRule = FileConditionRule (fn, fnRule)

    /// Creates phony action (check if I can unify the operator name)
    let (=>) name fnRule = PhonyRule (name,fnRule)

    /// Script builder.
    type RulesBuilder(options) =

        let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
        let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Targets = f(options.Targets)}, rules)

        member o.Bind(x,f) = f x
        member o.Zero() = XakeScript (options, Rules [])
        member o.Yield(())    = o.Zero()

        member this.Run(script) = Impl.run script

        [<CustomOperation("rule")>] member this.Rule(script, rule)                  = updRules script (Impl.addRule rule)
        [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action) = updRules script (pattern *> action |> Impl.addRule)
        [<CustomOperation("phony")>] member this.Phony(script, name, action)        = updRules script (name => action |> Impl.addRule)
        [<CustomOperation("rules")>] member this.Rules(script, rules)               = (rules |> List.map Impl.addRule |> List.fold (>>) id) |> updRules script

        [<CustomOperation("want")>] member this.Want(script, targets)               = updTargets script (function |[] -> targets | _ as x -> x)    // Options override script!
        [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets)= updTargets script (fun _ -> targets)

    /// key functions implementation follows

    /// <summary>
    /// Gets the script options.
    /// </summary>
    let getCtxOptions () = action {
        let! (ctx: ExecContext) = getCtx()
        return ctx.Options
    }

    /// <summary>
    /// Executes and awaits specified artifacts.
    /// </summary>
    /// <param name="targets"></param>
    let need targets =
        action {
            let! ctx = getCtx()
            let t' = targets |> (List.map (Impl.makeTarget ctx))
            do! Impl.need t'
        }

    let needFiles (Filelist files) =
        action {
            let targets = files |> List.map (fun f -> File.make f.FullName |> FileTarget)
            do! Impl.need targets
        }

    /// <summary>
    /// Instructs Xake to rebuild the target even if dependencies are not changed.
    /// </summary>
    let alwaysRerun() = action { let! result = getResult()
                                 do! setResult { result with Depends = Dependency.AlwaysRerun :: result.Depends } }


    /// <summary>
    /// Gets the environment variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getEnv variableName = action {
        let value = Impl.getEnvVar variableName

        // record the dependency
        let! result = getResult()
        do! setResult {result with Depends = Dependency.EnvVar (variableName,value) :: result.Depends}

        return value
    }

    /// <summary>
    /// Gets the global (options) variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getVar variableName = action {
        let! ctx = getCtx()
        let value = ctx.Options.Vars |> List.tryPick (Impl.valueByName variableName)

        // record the dependency
        let! result = getResult()
        do! setResult {result with Depends = Dependency.Var (variableName,value) :: result.Depends}

        return value
    }

    /// <summary>
    /// Gets the list of files matching specified fileset.
    /// </summary>
    /// <param name="fileset"></param>
    let getFiles fileset = action {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ [Dependency.GetFiles (fileset,files)]}

        return files
    }

    /// <summary>
    /// Writes a message to a log.
    /// </summary>
    let trace = Impl.traceLog

    [<System.Obsolete>]
    let writeLog = Impl.traceLog

    /// <summary>
    /// Gets state of particular target.
    /// Temporary method for analyzing changes impact.
    /// </summary>
    let getPlainDeps ctx =

        let rec getDeps:Target -> DepState list =
            (Impl.getDepsImpl ctx (fun x -> getDeps x))
            |> memoize

        Impl.getPlainDeps getDeps (Impl.getExecTime ctx)

    /// Defines a rule that demands specified targets
    /// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
    let (<==) name targets = PhonyRule (name,action {
        do! need targets
        do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
    })
    let (==>) = (<==)
