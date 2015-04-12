namespace Xake

[<AutoOpen>]
module XakeScript =

    open BuildLog
    open System.Threading

    type XakeOptionsType = {
        /// Defines project root folder
        ProjectRoot : string    // TODO DirectoryInfo?
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
        Want: string list
        Vars: (string * string) list

        /// Defines whether `run` should throw exception if script fails
        FailOnError: bool
    }

    type private ExecStatus = | Succeed | Skipped | JustFile
    type private TaskPool = Agent<WorkerPool.ExecMessage<ExecStatus>>

    type ExecContext = {
        TaskPool: TaskPool
        Db: Agent<Storage.DatabaseApi>
        Throttler: SemaphoreSlim
        Options: XakeOptionsType
        Rules: Rules<ExecContext>
        Logger: ILogger
        RootLogger: ILogger
        Ordinal: int
        NeedRebuild: Target -> bool
        Progress: Agent<Progress.ProgressReport>
    }

    /// Main type.
    type XakeScript = XakeScript of XakeOptionsType * Rules<ExecContext>

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
    let XakeOptions = {
        ProjectRoot = System.IO.Directory.GetCurrentDirectory()
        Threads = System.Environment.ProcessorCount
        ConLogLevel = Normal

        CustomLogger = CustomLogger (fun _ -> false) ignore
        FileLog = "build.log"
        FileLogLevel = Chatty
        Want = []
        FailOnError = false
        Vars = List<string*string>.Empty
        }

    module private Impl = begin
        open WorkerPool
        open BuildLog
        open Storage

        let nullableToOption = function | null -> None | s -> Some s
        let valueByName variableName = function |name,value when name = variableName -> Some value | _ -> None

        let TimeCompareToleranceMs = 100.0

        /// Writes the message with formatting to a log
        let writeLog (level:Logging.Level) fmt =
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
                    |FileConditionRule (f,_), FileTarget file when (f file.FullName) = true ->
                        //writeLog Level.Debug "Found conditional pattern '%s'" name
                        Some (rule)
                    |FileRule (pattern,_), FileTarget file when Fileset.matches pattern projectRoot file.FullName ->
                        // writeLog Verbose "Found pattern '%s' for %s" pattern (getShortname target)
                        Some (rule)
                    |PhonyRule (name,_), PhonyAction phony when phony = name ->
                        // writeLog Verbose "Found phony pattern '%s'" name
                        Some (rule)
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
        let newTaskContext ctx =
            let ordinal = System.Threading.Interlocked.Increment(refTaskOrdinal)
            let prefix = ordinal |> sprintf "%i> "
            in
            {ctx with Ordinal = ordinal; Logger = PrefixLogger prefix ctx.RootLogger}

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
            | File (a:Artifact, wrtime) when not(a.Exists && abs((a.LastWriteTime - wrtime).TotalMilliseconds) < TimeCompareToleranceMs) ->
                let afile = a in
                DepState.FilesChanged [afile.Name]

            | ArtifactDep (FileTarget file) when not file.Exists ->
                DepState.Other <| sprintf "target doesn't exist '%s'" file.Name

            | ArtifactDep dependeeTarget ->
                let ls = isOutdatedTarget dependeeTarget in

                if List.exists ((<>) DepState.NotChanged) ls then
                    DepState.Depends (dependeeTarget,ls)
                else
                    NotChanged
    
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

            | Some {BuildResult.Result = FileTarget file} when not file.Exists ->
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
                | DepState.FilesChanged file_list -> file_list |> take 5 |> DepState.FilesChanged
                | DepState.Depends (t,deps) -> DepState.Depends (t, deps |> List.map stripReasons)
                | _ as state -> state
            
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

            /// Collapses all instances of FileTarget to a single one
            let mergeDeps depends =
                depends
                |> List.partition (function |DepState.Depends (FileTarget _,_) -> true | _ -> false)
                |> fun (filesDep, rest) ->
                    let allFiles = filesDep |> List.map (fun (DepState.Depends (FileTarget t,ls)) -> [t.FullName]) |> List.concat
                    in
                    DepState.Refs allFiles :: rest
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
                        let taskContext = newTaskContext ctx                           
                        do ctx.Logger.Log Command "Started %s as task %i" (getShortname target) taskContext.Ordinal

                        let startResult = {BuildLog.makeResult target with Steps = [Step.start "all"]}
                        let! (result,_) = action (startResult,taskContext)
                        let result = Step.updateTotalDuration result

                        Store result |> ctx.Db.Post

                        do Progress.TaskComplete target |> ctx.Progress.Post
                        do ctx.Logger.Log Command "Completed %s in %A ms (wait %A ms)" (getShortname target) (Step.lastStep result).OwnTime  (Step.lastStep result).WaitTime
                        return ExecStatus.Succeed
                    | false ->
                        do ctx.Logger.Log Command "Skipped %s (up to date)" (getShortname target)
                        return ExecStatus.Skipped
                }

            let getAction = function
                | FileRule (_, action)
                | FileConditionRule (_, action) ->
                    let (FileTarget artifact) = target in
                    let (Action r) = action artifact in
                    Some r
                | PhonyRule (_, Action r) -> Some r

            // result expression is...
            target
            |> locateRule ctx.Rules ctx.Options.ProjectRoot
            |> Option.bind getAction
            |> function
                | Some action ->
                    async {
                        let! waitTask = (fun channel -> Run(target, run action, channel)) |> ctx.TaskPool.PostAndAsyncReply
                        let! status = waitTask
                        return status, Dependency.ArtifactDep target
                    }
                | None ->
                    target |> function
                    | FileTarget file when file.Exists ->
                        async {return ExecStatus.JustFile, Dependency.File (file, file.LastWriteTime)}
                    | _ -> raiseError ctx (sprintf "Neither rule nor file is found for '%s'" (getFullname target)) ""

        /// <summary>
        /// Executes several artifacts in parallel.
        /// </summary>
        and private execMany ctx = Seq.ofList >> Seq.map (execOne ctx) >> Async.Parallel

        /// <summary>
        /// Gets the status of dependency artifacts (obtained from 'need' calls).
        /// </summary>
        /// <returns>
        /// ExecStatus.Succeed,... in case at least one dependency was rebuilt
        /// </returns>
        and execNeed ctx targets : Async<ExecStatus * Dependency list> =
            async {
                ctx.Throttler.Release() |> ignore
                let! statuses = targets |> execMany ctx
                do! ctx.Throttler.WaitAsync(-1) |> Async.AwaitTask |> Async.Ignore

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
                FileTarget (Artifact (ctx.Options.ProjectRoot </> name))        

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
                NeedRebuild = fun _ -> false
                Progress = Progress.emptyProgress()
                }
            // TODO wrap more elegantly
            let rec get_changed_deps = CommonLib.memoize (getDepsImpl ctx (fun x -> get_changed_deps x)) in

            let check_rebuild target =
                get_changed_deps >>
                function
                | [] -> false, ""
                | DepState.Other reason::_            -> true, reason
                | DepState.Depends (t,_) ::_          -> true, "Depends on target " + (getFullName t)
                | DepState.FilesChanged (file::_) ::_ -> true, "File(s) changed " + file
                | reasons -> true, sprintf "Some reason %A" reasons
                >>
                function
                | false, _ -> false
                | true, reason ->
                    do ctx.Logger.Log Info "Rebuild %A: %s" (getShortname target) reason
                    true
                <| target

            // define targets
            let targets =
                match options.Want with
                | [] ->
                    do logger.Log Level.Message "No target(s) specified. Defaulting to 'main'"
                    ["main"]
                | tt -> tt
                |> List.map (makeTarget ctx)

            let getDurationDeps t =
                let collectTargets = List.collect (function |Depends (t,_) -> [t] | _ -> [])
                (getExecTime ctx t) / 1000<ms>, get_changed_deps t |> collectTargets
            let progressSink = Progress.openProgress getDurationDeps options.Threads targets

            let ctx = {ctx with NeedRebuild = check_rebuild; Progress = progressSink}

            logger.Log Info "Options: %A" options

            let rec unwindAggEx (e:System.Exception) = seq {
                match e with
                    | :? System.AggregateException as a -> yield! a.InnerExceptions |> Seq.collect unwindAggEx
                    | a -> yield a
                }

            try
                try
                    targets |> execMany ctx |> Async.RunSynchronously |> ignore
                    logger.Log Message "\n\n\tBuild completed in %A\n" (System.DateTime.Now - start)
                with 
                    | exn ->
                        let th = if options.FailOnError then raiseError else reportError
                        let errors = exn |> unwindAggEx |> Seq.map (fun e -> e.Message) in
                        th ctx (exn.Message + "\n" + (errors |> String.concat "\r\n            ")) exn
                        logger.Log Message "\n\n\tBuild failed after running for %A\n" (System.DateTime.Now - start)
            finally
                db.PostAndReply Storage.CloseWait
                do Progress.Finish |> ctx.Progress.Post

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
    let ( *> ) pattern fnRule = FileRule (pattern, fnRule)
    let ( *?> ) fn fnRule = FileConditionRule (fn, fnRule)

    /// Creates phony action (check if I can unify the operator name)
    let (=>) name fnRule = PhonyRule (name,fnRule)

    /// Script builder.
    type RulesBuilder(options) =

        let updRules (XakeScript (options,rules)) f = XakeScript (options, f(rules))
        let updTargets (XakeScript (options,rules)) f = XakeScript ({options with Want = f(options.Want)}, rules)

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

    /// creates xake build script
    let xake options =
        new RulesBuilder(options)

    /// Create xake build script using command-line arguments to define script options
    let xakeArgs args options =
        let _::targets = Array.toList args
        // this is very basic implementation which only recognizes target names
        // TODO support global variables (with dependency tracking)
        // TODO support sequential/parallel runs e.g. "clean release-build;debug-build"
        new RulesBuilder({options with Want = targets})

    /// Gets the script options.
    let getCtxOptions() = action {
        let! (ctx: ExecContext) = getCtx()
        return ctx.Options
    }

    /// key functions implementation

    /// Executes and awaits specified artifacts
    let need targets =
            action {
                let! ctx = getCtx()
                let t' = targets |> (List.map (Impl.makeTarget ctx))

                do! Impl.need t'
            }

    let needFiles (Filelist files) =
            action {
                let! ctx = getCtx()
                let targets = files |> List.map (fun f -> new Artifact (f.FullName) |> FileTarget)

                do! Impl.need targets
         }

    /// Instructs Xake to rebuild the target even if dependencies are not changed
    let alwaysRerun () = action {
        let! ctx = getCtx()
        let! result = getResult()
        do! setResult {result with Depends = Dependency.AlwaysRerun :: result.Depends}
    }

    /// Gets the environment variable
    let getEnv variableName = action {
        let! ctx = getCtx()

        let value = Impl.getEnvVar variableName

        // record the dependency
        let! result = getResult()
        do! setResult {result with Depends = Dependency.EnvVar (variableName,value) :: result.Depends}

        return value
    }

    /// Gets the global variable
    let getVar variableName = action {
        let! ctx = getCtx()
        let value = ctx.Options.Vars |> List.tryPick (Impl.valueByName variableName)
        
        // record the dependency
        let! result = getResult()
        do! setResult {result with Depends = Dependency.Var (variableName,value) :: result.Depends}

        return value
    }

    /// Executes and awaits specified artifacts
    let getFiles fileset = action {
        let! ctx = getCtx()
        let files = fileset |> toFileList ctx.Options.ProjectRoot

        let! result = getResult()
        do! setResult {result with Depends = result.Depends @ [Dependency.GetFiles (fileset,files)]}

        return files
    }

    /// Writes a message to a log
    let writeLog = Impl.writeLog

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
