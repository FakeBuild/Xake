namespace Xake

module BuildLog = 
    open Xake
    open System
    
    let XakeDbVersion = "0.4"
    
    type Database = { Status : Map<Target, BuildResult> }
    
    (* API *)

    /// Creates a new build result
    let makeResult target = 
        { Targets = target
          Built = DateTime.Now
          Depends = []
          Steps = [] }
    
    /// Creates a new database
    let newDatabase() = { Database.Status = Map.empty }
    
    /// Adds result to a database
    let internal addResult db result =
        { db with Status = result.Targets |> List.fold (fun m i -> Map.add i result m) db.Status }

type 't Agent = 't MailboxProcessor

module Storage = 
    open Xake
    open BuildLog
    
    module private Persist = 
        open Pickler

        type DatabaseHeader = 
            { XakeSign : string
              XakeVer : string
              ScriptDate : Timestamp }
        
        let file = wrap (File.make, fun a -> a.FullName) str
        
        let target = 
            alt (function 
                | FileTarget _ -> 0
                | PhonyAction _ -> 1) 
                [|  wrap (File.make >> FileTarget, fun (FileTarget f | OtherwiseFail f) -> f.Name) str
                    wrap (PhonyAction, (fun (PhonyAction a | OtherwiseFail a) -> a)) str |]
        
        let step = 
            wrap 
                ((fun (n, s, o, w) -> {StepInfo.Name = n; Start = s; OwnTime = o * 1<ms>; WaitTime = w * 1<ms>}), 
                 fun ({StepInfo.Name = n; Start = s; OwnTime = o; WaitTime = w}) -> (n, s, o / 1<ms>, w / 1<ms>)) (quad str date int int)
        
        // Fileset of FilesetOptions * FilesetElement list
        let dependency = 
            alt (function 
                | ArtifactDep _ -> 0
                | FileDep _ -> 1
                | EnvVar _ -> 2
                | Var _ -> 3
                | AlwaysRerun _ -> 4
                | GetFiles _ -> 5) 
                [| wrap (ArtifactDep, fun (ArtifactDep f | OtherwiseFail f) -> f) target                   
                   wrap (FileDep, fun (FileDep(f, ts) | OtherwiseFail (f, ts)) -> (f, ts))  (pair file date)                   
                   wrap (EnvVar, fun (EnvVar(n, v) | OtherwiseFail (n,v)) -> n, v)  (pair str (option str))
                   wrap (Var, fun (Var(n, v)| OtherwiseFail (n,v)) -> n, v)        (pair str (option str))
                   wrap0 AlwaysRerun                   
                   wrap (GetFiles, fun (GetFiles(fs, fi)| OtherwiseFail (fs,fi)) -> fs, fi)  (pair filesetPickler filelistPickler) |]
        
        let result = 
            wrap 
                ((fun (r, built, deps, steps) -> 
                 { Targets = r
                   Built = built
                   Depends = deps
                   Steps = steps }), 
                 fun r -> (r.Targets, r.Built, r.Depends, r.Steps)) 
                (quad (list target) date (list dependency) (list step))
        
        let dbHeader = 
            wrap 
                ((fun (sign, ver, scriptDate) -> 
                 { DatabaseHeader.XakeSign = sign
                   XakeVer = ver
                   ScriptDate = scriptDate }), 
                 fun h -> (h.XakeSign, h.XakeVer, h.ScriptDate)) 
                (triple str str date)
    
    module private impl = 
        open System.IO
        open Persist
        
        let writeHeader w = 
            let h = 
                { DatabaseHeader.XakeSign = "XAKE"
                  XakeVer = XakeDbVersion
                  ScriptDate = System.DateTime.Now }
            Persist.dbHeader.pickle h w
        
        let openDatabaseFile dbpath (logger : ILogger) = 
            let log = logger.Log
            let resultPU = Persist.result
            let bkpath = dbpath <.> "bak"
            // if exists backup restore
            if File.Exists(bkpath) then 
                log Level.Message "Backup file found ('%s'), restoring db" 
                    bkpath
                try 
                    File.Delete(dbpath)
                with _ -> ()
                File.Move(bkpath, dbpath)
            let db = ref (newDatabase())
            let recordCount = ref 0
            // read database
            if File.Exists(dbpath) then 
                try 
                    use reader = new BinaryReader(File.OpenRead(dbpath))
                    let stream = reader.BaseStream
                    let header = Persist.dbHeader.unpickle reader
                    if header.XakeVer < XakeDbVersion then 
                        failwith "Database version is old."
                    while stream.Position < stream.Length do
                        let result = resultPU.unpickle reader
                        db := result |> addResult !db
                        recordCount := !recordCount + 1
                // if fails create new
                with ex -> 
                    log Level.Error 
                        "Failed to read database, so recreating. Got \"%s\"" 
                    <| ex.ToString()
                    try 
                        File.Delete(dbpath)
                    with _ -> ()
            // check if we can cleanup db
            if !recordCount > (!db).Status.Count * 5 then 
                log Level.Message "Compacting database"
                File.Move(dbpath, bkpath)
                use writer = 
                    new BinaryWriter(File.Open(dbpath, FileMode.CreateNew))
                writeHeader writer
                (!db).Status
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.iter (fun r -> resultPU.pickle r writer)
                File.Delete(bkpath)
            let dbwriter = 
                new BinaryWriter(File.Open (dbpath, FileMode.Append, FileAccess.Write))
            if dbwriter.BaseStream.Position = 0L then writeHeader dbwriter
            db, dbwriter
    
    type DatabaseApi = 
        | GetResult of Target * AsyncReplyChannel<Option<BuildResult>>
        | Store of BuildResult
        | Close
        | CloseWait of AsyncReplyChannel<unit>
    
    /// <summary>
    /// Build result pickler.
    /// </summary>
    let resultPU = Persist.result
    
    /// <summary>
    /// Opens database.
    /// </summary>
    /// <param name="dbpath">Full xake database file name</param>
    /// <param name="logger"></param>
    let openDb dbpath (logger : ILogger) = 
        let db, dbwriter = impl.openDatabaseFile dbpath logger
        MailboxProcessor.Start(fun mbox -> 
            let rec loop (db) = 
                async { 
                    let! msg = mbox.Receive()
                    match msg with
                    | GetResult(key, chnl) -> 
                        db.Status
                        |> Map.tryFind key
                        |> chnl.Reply
                        return! loop (db)
                    | Store result -> 
                        Persist.result.pickle result dbwriter
                        return! loop (result |> addResult db)
                    | Close -> 
                        logger.Log Info "Closing database"
                        dbwriter.Dispose()
                        return ()
                    | CloseWait ch -> 
                        logger.Log Info "Closing database"
                        dbwriter.Dispose()
                        ch.Reply()
                        return ()
                }
            loop (!db))

/// Utility methods to manipulate build stats
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
