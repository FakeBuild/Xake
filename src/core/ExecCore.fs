﻿module internal Xake.ExecCore

open System.Text.RegularExpressions

open Xake
open DependencyAnalysis
open Database

/// Writes the message with formatting to a log
let traceLog (level:Logging.Level) fmt =
    let write s = recipe {
        let! ctx = getCtx()
        return ctx.Logger.Log level "%s" s
    }
    Printf.kprintf write fmt

let wildcardsRegex = Regex(@"\*\*|\*|\?", RegexOptions.Compiled)
let patternTagRegex = Regex(@"\((?'tag'\w+?)\:[^)]+\)", RegexOptions.Compiled)
let replace (regex:Regex) (evaluator: Match -> string) text = regex.Replace(text, evaluator)
let ifNone x = function |Some x -> x | _ -> x

let (|Dump|Dryrun|Run|) (opts:ExecOptions) =
    match opts with
    | _ when opts.DumpDeps -> Dump
    | _ when opts.DryRun -> Dryrun
    | _ -> Run

let applyWildcards = function
    | None -> id
    | Some matches ->
        fun pat ->
            let mutable i = 0
            let evaluator m =
                i <- i + 1
                matches |> Map.tryFind (i.ToString()) |> ifNone ""
            let evaluatorTag (m: Match) =
                matches |> (Map.tryFind m.Groups.["tag"].Value) |> ifNone ""
            pat
            |> replace wildcardsRegex evaluator
            |> replace patternTagRegex evaluatorTag
            
// locates the rule
let locateRule (Rules rules) projectRoot target =
    let matchRule rule =
        match rule, target with

        |FileConditionRule (meetCondition,_), FileTarget file when file |> File.getFullName |> meetCondition ->
            //writeLog Level.Debug "Found conditional pattern '%s'" name
            // TODO let condition rule extracting named groups
            Some (rule,[],[target])

        |FileRule (pattern,_), FileTarget file ->
            file
            |> File.getFullName
            |> Path.matchGroups pattern projectRoot
            |> Option.map (fun groups -> rule,groups,[target])

        |MultiFileRule (patterns, _), FileTarget file ->
            let fname = file |> File.getFullName
            patterns
            |> List.tryPick(fun pattern ->
                Path.matchGroups pattern projectRoot fname
                |> Option.map(fun groups -> groups, pattern)
                )
            |> Option.map (fun (groups, _) ->
                let generateName = applyWildcards (Map.ofList groups |> Some)
                
                let targets = patterns |> List.map (generateName >> (</>) projectRoot >> File.make >> FileTarget)
                rule, groups, targets)

        |PhonyRule (name,_), PhonyAction phony when phony = name ->
            // writeLog Verbose "Found phony pattern '%s'" name
            Some (rule, [], [target])

        | _ -> None

    rules |> List.tryPick matchRule

let reportError ctx error details =
    do ctx.Logger.Log Error "Error '%s'. See build.log for details" error
    do ctx.Logger.Log Verbose "Error details are:\n%A\n\n" details

let raiseError ctx error details =
    do reportError ctx error details
    raise (XakeException(sprintf "Script failed (error code: %A)\n%A" error details))

// Ordinal of the task being added to a task pool
let refTaskOrdinal = ref 0

/// Creates a context for a new task
let newTaskContext targets matches ctx =
    let ordinal = System.Threading.Interlocked.Increment(refTaskOrdinal)
    let prefix = ordinal |> sprintf "%i> "
    in
    { ctx with
        Ordinal = ordinal; Logger = PrefixLogger prefix ctx.RootLogger
        Targets = targets
        RuleMatches = matches }

// executes single artifact
let rec execOne ctx target =

    let run ruleMatches (Recipe action) targets =
        let primaryTarget = targets |> List.head
        async {
            match ctx.NeedRebuild targets with
            | true ->
                let taskContext = newTaskContext targets ruleMatches ctx
                do ctx.Logger.Log Command "Started %s as task %i" (Target.shortName primaryTarget) taskContext.Ordinal

                do Progress.TaskStart primaryTarget |> ctx.Progress.Post

                let startResult = {BuildResult.makeResult targets with Steps = [BuildResult.startStep "all"]}
                let! ({Result = result},_) = action { taskContext with Result = startResult }
                let result = BuildResult.updateTotalDuration result

                Store (result.Targets, result) |> ctx.Db.Post

                do Progress.TaskComplete primaryTarget |> ctx.Progress.Post
                do ctx.Logger.Log Command "Completed %s in %A ms (wait %A ms)" (Target.shortName primaryTarget) (BuildResult.lastStep result).OwnTime  (BuildResult.lastStep result).WaitTime
                return Succeed
            | false ->
                do ctx.Logger.Log Command "Skipped %s (up to date)" (Target.shortName primaryTarget)
                return Skipped
        }

    let getAction = function
        | FileRule (_, a)
        | FileConditionRule (_, a)
        | MultiFileRule (_, a)
        | PhonyRule (_, a) -> a

    // result expression is...
    match target |> locateRule ctx.Rules ctx.Options.ProjectRoot with
    | Some(rule,groups,targets) ->
        let groupsMap = groups |> Map.ofSeq
        async {
            let taskTitle = Target.shortName target
            let! waitTask = (fun channel -> WorkerPool.Run(taskTitle, target::targets, run groupsMap (getAction rule) targets, channel)) |> ctx.Workers.PostAndAsyncReply
            let! status = waitTask
            return target, status, ArtifactDep target
        }
    | None ->
        target |> function
        | FileTarget file when File.exists file ->
            async.Return <| (target, JustFile, FileDep (file, File.getLastWriteTime file))
        | _ -> raiseError ctx (sprintf "Neither rule nor file is found for '%s'" <| Target.fullName target) ""

/// Executes several artifacts in parallel.
and execParallel ctx = List.map (execOne ctx) >> Seq.ofList >> Async.Parallel

/// Gets the status of dependency artifacts (obtained from 'need' calls).
/// <returns>
/// ExecStatus.Succeed,... in case at least one dependency was rebuilt
/// </returns>
and execNeed ctx targets : Async<ExecStatus * Dependency list> =
    async {
        let primaryTarget = ctx.Targets |> List.head
        primaryTarget |> (Progress.TaskSuspend >> ctx.Progress.Post)

        do ctx.Throttler.Release() |> ignore
        let! statuses = targets |> execParallel ctx
        do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore

        primaryTarget |> (Progress.TaskResume >> ctx.Progress.Post)

        let dependencies = statuses |> Array.map (fun (_,_,x) -> x) |> List.ofArray in
        return
            (match statuses |> Array.exists (fun (_,x,_) -> x = ExecStatus.Succeed) with
                |true -> Succeed
                |false -> Skipped), dependencies
    }

/// phony actions are detected by their name so if there's "clean" phony and file "clean" in `need` list if will choose first
let makeTarget ctx name =
    let (Rules rules) = ctx.Rules
    let isPhonyRule nm = function |PhonyRule (n,_) when n = nm -> true | _ -> false
    in
    match rules |> List.exists (isPhonyRule name) with
    | true -> PhonyAction name
    | _ -> ctx.Options.ProjectRoot </> name |> File.make |> FileTarget

/// Implementation of "dry run"
let dryRun ctx options (groups: string list list) =
    let getDeps = getChangeReasons ctx |> memoizeRec

    // getPlainDeps getDeps (getExecTime ctx)
    do ctx.Logger.Log Command "Running (dry) targets %A" groups
    let doneTargets = System.Collections.Hashtable()

    let print f = ctx.Logger.Log Info f
    let indent i = String.replicate i "  "

    let rec showDepStatus ii reasons =
        reasons |> function
        | Other reason ->   print "%sReason: %s" (indent ii) reason
        | Depends t ->      print "%sDepends '%s' - changed target" (indent ii) (Target.shortName t)
        | DependsMissingTarget t ->      print "%sDepends on '%s' - missing target" (indent ii) (Target.shortName t)
        | FilesChanged (file:: rest) ->  print "%sFile is changed '%s' %s" (indent ii) file (if List.isEmpty rest then "" else sprintf " and %d more file(s)" <| List.length rest)
        | reasons ->        print "%sSome reason %A" (indent ii) reasons

    let rec displayNestedDeps ii =
        function
        | DependsMissingTarget t
        | Depends t ->
            showTargetStatus ii t
        | _ -> ()
    and showTargetStatus ii target =
        if not <| doneTargets.ContainsKey(target) then
            doneTargets.Add(target, 1)
            let deps = getDeps target
            if not <| List.isEmpty deps then
                let execTimeEstimate = getExecTime ctx target
                do ctx.Logger.Log Command "%sRebuild %A (~%Ams)" (indent ii) (Target.shortName target) execTimeEstimate
                deps |> List.iter (showDepStatus (ii+1))
                deps |> List.iter (displayNestedDeps (ii+1))

    let targetGroups = makeTarget ctx |> List.map |> List.map <| groups in 
    let toSec v = float (v / 1<ms>) * 0.001
    let endTime = Progress.estimateEndTime (getDurationDeps ctx getDeps) options.Threads targetGroups |> toSec

    targetGroups |> List.collect id |> List.iter (showTargetStatus 0)
    match targetGroups |> List.collect id |> List.collect getDeps with
    | [] ->
        ctx.Logger.Log Message "\n\n\tNo changed dependencies. Nothing to do.\n"
    | _ ->
        let parallelismMsg =
            let endTimeTotal = Progress.estimateEndTime (getDurationDeps ctx getDeps) 1 targetGroups |> toSec
            if options.Threads > 1 && endTimeTotal > endTime * 1.05 then
                sprintf "\n\tTotal tasks duration is (estimate) in %As\n\tParallelism degree: %.2f" endTimeTotal (endTimeTotal / endTime)
            else ""
        ctx.Logger.Log Message "\n\n\tBuild will be completed (estimate) in %As%s\n" endTime parallelismMsg

let rec unwindAggEx (e:System.Exception) = seq {
    match e with
        | :? System.AggregateException as a -> yield! a.InnerExceptions |> Seq.collect unwindAggEx
        | a -> yield a
    }

let runSeq<'r> :Async<'r> list -> Async<'r list> = 
    List.fold
        (fun rest i -> async {
            let! tail = rest
            let! head = i
            return head::tail
        })
        (async {return []})

let asyncMap f c = async.Bind(c, f >> async.Return)

/// Runs the build (main function of xake)
let runBuild ctx options groups =

    let runTargets ctx options targets =
        let getDeps = getChangeReasons ctx |> memoizeRec
        
        let needRebuild (target: Target) =
            getDeps >>
            function
            | [] -> false, ""
            | Other reason::_        -> true, reason
            | Depends t ::_          -> true, "Depends on target " + (Target.shortName t)
            | DependsMissingTarget t ::_ -> true, sprintf "Depends on target %s (missing)" (Target.shortName t)
            | FilesChanged (file::_) ::_ -> true, "File(s) changed " + file
            | reasons -> true, sprintf "Some reason %A" reasons
            >>
            function
            | false, _ -> false
            | true, reason ->
                do ctx.Logger.Log Info "Rebuild %A: %s" (Target.shortName target) reason
                true
            <| target
            // todo improve output by printing primary target

        async {
            do ctx.Logger.Log Info "Build target list %A" targets

            let progressSink = Progress.openProgress (getDurationDeps ctx getDeps) options.Threads targets options.Progress
            let stepCtx = {ctx with NeedRebuild = List.exists needRebuild; Progress = progressSink}

            try
                return! targets |> execParallel stepCtx
            finally
                do Progress.Finish |> progressSink.Post
        }

    groups |> List.map
        (List.map (makeTarget ctx) >> (runTargets ctx options))
    |> runSeq
    |> asyncMap (Array.concat >> List.ofArray)

/// Executes the build script
let runScript options rules =
    let logger = CombineLogger (ConsoleLogger options.ConLogLevel) options.CustomLogger

    let logger =
        match options.FileLog, options.FileLogLevel with
        | null,_ | "",_
        | _, Silent -> logger
        | logFileName,level -> CombineLogger logger (FileLogger logFileName level)

    let (throttler, pool) = WorkerPool.create logger options.Threads

    let db = BuildDatabase.openDb (options.ProjectRoot </> options.DbFileName) logger

    let ctx = {
        Ordinal = 0
        Workers = pool; Throttler = throttler
        Options = options; Rules = rules
        Logger = logger; RootLogger = logger; Db = db
        Progress = Progress.emptyProgress()
        NeedRebuild = fun _ -> false
        Targets = []
        RuleMatches = Map.empty
        Result = BuildResult.makeResult [] }

    logger.Log Info "Options: %A" options

    // splits list of targets ["t1;t2"; "t3;t4"] into list of list.
    let targetLists =
        options.Targets |>
        function
        | [] ->
            do logger.Log Message "No target(s) specified. Defaulting to 'main'"
            [["main"]]
        | tt ->
            tt |> List.map (fun (s: string) -> s.Split(';', '|') |> List.ofArray)

    try
        match options with
        | Dump ->
            do logger.Log Command "Dumping dependencies for targets %A" targetLists
            targetLists |> List.iter (List.map (makeTarget ctx) >> (dumpDeps ctx))
        | Dryrun ->
            targetLists |> (dryRun ctx options)
        | _ ->
            let start = System.DateTime.Now
            try
                targetLists |> (runBuild ctx options) |> Async.RunSynchronously |> ignore
                ctx.Logger.Log Message "\n\n    Build completed in %A\n" (System.DateTime.Now - start)
            with | exn ->
                let th = if options.FailOnError then raiseError else reportError
                let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) in
                th ctx (exn.Message + "\n" + (errors |> String.concat "\r\n            ")) exn
                ctx.Logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
                exit 2
    finally
        db.PostAndReply Database.CloseWait
        FlushLogs()

/// "need" implementation
let need targets = recipe {
    let startTime = System.DateTime.Now

    let! ctx = getCtx()
    let! _,deps = targets |> execNeed ctx

    let totalDuration = int (System.DateTime.Now - startTime).TotalMilliseconds * 1<ms>
    let result' = {ctx.Result with Depends = ctx.Result.Depends @ deps} |> (BuildResult.updateWaitTime totalDuration)
    do! setCtx { ctx with Result = result' }
}