namespace Xake

open Xake

[<AutoOpen>]
module XakeScript =

    open DependencyAnalysis

    /// Main type.
    type XakeScript = XakeScript of ExecOptions * Rules<ExecContext>

    /// Default options
    [<System.Obsolete("Obsolete, use ExecOptions.Default")>]
    let XakeOptions = ExecOptions.Default

    module private Impl = begin
        open WorkerPool
        open Storage

        /// Writes the message with formatting to a log
        let traceLog (level:Logging.Level) fmt =
            let write s = action {
                let! ctx = getCtx()
                return ctx.Logger.Log level "%s" s
            }
            Printf.kprintf write fmt

        // Ordinal of the task being added to a task pool
        let refTaskOrdinal = ref 0

        // locates the rule
        let locateRule (Rules rules) projectRoot target =
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

        let reportError ctx error details =
            do ctx.Logger.Log Error "Error '%s'. See build.log for details" error
            do ctx.Logger.Log Verbose "Error details are:\n%A\n\n" details

        let raiseError ctx error details =
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

        // executes single artifact
        let rec execOne ctx target =

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
        and execParallel ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

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
                |> (function |true -> ExecStatus.Succeed |false -> ExecStatus.Skipped), dependencies
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

        /// Implementation of "dry run"
        let dryRun ctx options (groups: string list list) =
            let rec getDeps = getChangeReasons ctx (fun x -> getDeps x) |> memoize

            // getPlainDeps getDeps (getExecTime ctx)
            do ctx.Logger.Log Command "Running (dry) targets %A" groups
            let doneTargets = new System.Collections.Hashtable()

            let print f = ctx.Logger.Log Info f
            let indent i = String.replicate i "  "

            let rec showDepStatus ii reasons =
                reasons |> function
                | ChangeReason.Other reason ->
                    print "%sReason: %s" (indent ii) reason
                | ChangeReason.Depends t ->
                    print "%sDepends '%s' - changed target" (indent ii) t.ShortName
                | ChangeReason.DependsMissingTarget t ->
                    print "%sDepends on '%s' - missing target" (indent ii) t.ShortName
                | ChangeReason.FilesChanged (file:: rest) ->
                    print "%sFile is changed '%s' %s" (indent ii) file (if List.isEmpty rest then "" else sprintf " and %d more file(s)" <| List.length rest)
                | reasons ->
                    do print "%sSome reason %A" (indent ii) reasons
                ()
            let rec displayNestedDeps ii =
                function
                | ChangeReason.DependsMissingTarget t
                | ChangeReason.Depends t ->
                    showTargetStatus ii t
                | _ -> ()
            and showTargetStatus ii target =
                if not <| doneTargets.ContainsKey(target) then
                    doneTargets.Add(target, 1)
                    let deps = getDeps target
                    if not <| List.isEmpty deps then
                        let execTimeEstimate = getExecTime ctx target
                        do ctx.Logger.Log Command "%sRebuild %A (~%Ams)" (indent ii) target.ShortName execTimeEstimate
                        deps |> List.iter (showDepStatus (ii+1))
                        deps |> List.iter (displayNestedDeps (ii+1))

            let targetGroups = groups |> List.map (List.map (makeTarget ctx)) in 
            let toSec v = float (v / 1<ms>) * 0.001
            let endTime = Progress.estimateEndTime2 (getDurationDeps ctx getDeps) options.Threads targetGroups |> toSec

            targetGroups |> List.collect id |> List.iter (showTargetStatus 0)

            let parallelismMsg =
                let endTimeTotal = Progress.estimateEndTime2 (getDurationDeps ctx getDeps) 1 targetGroups |> toSec
                if options.Threads > 1 && endTimeTotal > endTime then
                    sprintf "\n\tTotal tasks duration is (estimate) in %As\n\tParallelist degree: %.2f" endTimeTotal (endTimeTotal / endTime)
                else ""
            ctx.Logger.Log Message "\n\n\tBuild will be completed (estimate) in %As%s\n" endTime parallelismMsg

        let runTargets ctx options targets =
            let rec getDeps = getChangeReasons ctx (fun x -> getDeps x) |> memoize
            
            let check_rebuild (target: Target) =
                getDeps >>
                function
                | [] -> false, ""
                | ChangeReason.Other reason::_            -> true, reason
                | ChangeReason.Depends t ::_          -> true, "Depends on target " + t.ShortName
                | ChangeReason.DependsMissingTarget t ::_          -> true, sprintf "Depends on target %s (missing)" t.ShortName
                | ChangeReason.FilesChanged (file::_) ::_ -> true, "File(s) changed " + file
                | reasons -> true, sprintf "Some reason %A" reasons
                >>
                function
                | false, _ -> false
                | true, reason ->
                    do ctx.Logger.Log Info "Rebuild %A: %s" target.ShortName reason
                    true
                <| target

            let progressSink = Progress.openProgress (getDurationDeps ctx getDeps) options.Threads targets
            let stepCtx = {ctx with NeedRebuild = check_rebuild; Progress = progressSink}

            try
                targets |> execParallel stepCtx |> Async.RunSynchronously |> ignore
            finally
                do Progress.Finish |> progressSink.Post

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
            let db = Storage.openDb (options.ProjectRoot </> options.DbFileName) logger

            let ctx = {
                Ordinal = 0
                TaskPool = pool; Throttler = throttler
                Options = options; Rules = rules
                Logger = logger; RootLogger = logger; Db = db
                Progress = Progress.emptyProgress()
                NeedRebuild = fun _ -> false
                Tgt = None
                }

            logger.Log Info "Options: %A" options

            let rec unwindAggEx (e:System.Exception) = seq {
                match e with
                    | :? System.AggregateException as a -> yield! a.InnerExceptions |> Seq.collect unwindAggEx
                    | a -> yield a
                }

            // splits list of targets ["t1;t2"; "t3;t4"] into list of list.
            let targetLists =
                options.Targets |>
                function
                | [] ->
                    do logger.Log Level.Message "No target(s) specified. Defaulting to 'main'"
                    [["main"]]
                | tt ->
                    tt |> List.map (fun (s: string) -> s.Split(';', '|') |> List.ofArray)
            try
                if options.DumpDeps then
                    do logger.Log Level.Command "Dumping dependencies for targets %A" targetLists
                    targetLists |> List.iter (List.map (makeTarget ctx) >> (dumpDeps ctx))
                else if options.DryRun then
                    targetLists |> (dryRun ctx options)
                else
                    try
                        targetLists |> List.iter (List.map (makeTarget ctx) >> (runTargets ctx options))
                        logger.Log Message "\n\n\tBuild completed in %A\n" (System.DateTime.Now - start)
                    with
                        | exn ->
                            let th = if options.FailOnError then raiseError else reportError
                            let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) in
                            th ctx (exn.Message + "\n" + (errors |> String.concat "\r\n            ")) exn
                            logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
                            exit 2
            finally
                db.PostAndReply Storage.CloseWait
                Logging.FlushLogs()

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
    let ( %> ) pattern fnRule = FileRule (pattern, fnRule)
    let ( *> ) pattern fnRule = FileRule (pattern, fun (RuleActionArgs (t,_)) -> fnRule t)
    let ( *?> ) fn fnRule = FileConditionRule (fn, fnRule)

    /// Creates phony action (check if I can unify the operator name)
    let (=>) name fnRule = PhonyRule (name,fnRule)

    /// Script builder.
    type RulesBuilder(options) =

        let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
        let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Targets = f(options.Targets)}, rules)
        let addRule rule (Rules rules) :Rules<_> =    Rules (rule :: rules)

        let updateVar (key: string) (value: string) =
            List.filter(fst >> ((<>) key)) >> ((@) [key, value])

        member o.Bind(x,f) = f x
        member o.Zero() = XakeScript (options, Rules [])
        member o.Yield(())    = o.Zero()

        member this.Run(script) = Impl.run script

        [<CustomOperation("dryrun")>] member this.DryRun(XakeScript (options, rules))
            = XakeScript ({options with DryRun = true}, rules)

        [<CustomOperation("var")>] member this.AddVar(XakeScript (options, rules), name, value)
            =
            XakeScript ({options with Vars = options.Vars |> updateVar name value }, rules)

        [<CustomOperation("filelog")>] member this.FileLog(XakeScript (options, rules), filename, ?loglevel)
            =
            let loglevel = defaultArg loglevel Verbosity.Chatty in
            XakeScript ({options with FileLog = filename; FileLogLevel = loglevel}, rules)

        [<CustomOperation("rule")>] member this.Rule(script, rule)
            = updRules script (addRule rule)
        [<CustomOperation("addRule")>] member this.AddRule(script, pattern, action)
            = updRules script (pattern *> action |> addRule)
        [<CustomOperation("phony")>] member this.Phony(script, name, action)
            = updRules script (name => action |> addRule)
        [<CustomOperation("rules")>] member this.Rules(script, rules)
            = (rules |> List.map addRule |> List.fold (>>) id) |> updRules script

        [<CustomOperation("want")>] member this.Want(script, targets)
            = updTargets script (function |[] -> targets |x -> x)    // Options override script!
        [<CustomOperation("wantOverride")>] member this.WantOverride(script,targets)
            = updTargets script (fun _ -> targets)

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
        files |> List.map FileTarget |> Impl.need

    /// <summary>
    /// Instructs Xake to rebuild the target even if dependencies are not changed.
    /// </summary>
    let alwaysRerun() = action {
        let! result = getResult()
        do! setResult { result with Depends = Dependency.AlwaysRerun :: result.Depends }
    }

    /// <summary>
    /// Gets the environment variable.
    /// </summary>
    /// <param name="variableName"></param>
    let getEnv variableName =
        let value = Util.getEnvVar variableName
        action {
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
        let value = Util.getVar ctx.Options variableName

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

    [<System.Obsolete("Use trace instead")>]
    let writeLog = Impl.traceLog

    /// Defines a rule that demands specified targets
    /// e.g. "main" ==> ["build-release"; "build-debug"; "unit-test"]
    let (<==) name targets = PhonyRule (name,action {
        do! need targets
        do! alwaysRerun()   // always check demanded dependencies. Otherwise it wan't check any target is available
    })
    let (==>) = (<==)
