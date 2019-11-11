module Xake.Database

open Xake

let XakeDbVersion = "0.5"

type Database<'result> = { Status : Map<Target, 'result> }

type DatabaseHeader = 
    { XakeSign : string
      XakeVer : string
      ScriptDate : System.DateTime }

(* API *)

/// Creates a new database
let newDatabase() = { Database.Status = Map.empty }

/// Adds result to a database
let internal addResult db targets (result: 'result) =
    { db with Status = targets |> List.fold (fun m i -> Map.add i result m) db.Status }

module Picklers =
    open Pickler

    let targetPu = 
        alt (function 
            | FileTarget _ -> 0
            | PhonyAction _ -> 1) 
            [|  wrap (File.make >> FileTarget, fun (FileTarget f | OtherwiseFail f) -> f.Name) str
                wrap (PhonyAction, (fun (PhonyAction a | OtherwiseFail a) -> a)) str |]

    let dbHeaderPu = 
        wrap 
            ((fun (sign, ver, scriptDate) -> 
             { DatabaseHeader.XakeSign = sign
               XakeVer = ver
               ScriptDate = scriptDate }), 
             fun h -> (h.XakeSign, h.XakeVer, h.ScriptDate)) 
            (triple str str date)

module private impl = 
    open System.IO
    open Pickler
    open Picklers
    
    let writeHeader w = 
        let h = 
            { DatabaseHeader.XakeSign = "XAKE"
              XakeVer = XakeDbVersion
              ScriptDate = System.DateTime.Now }
        dbHeaderPu.pickle h w

    let targetListPu = list targetPu
    
    let openDatabaseFile (resultPU: 'result PU) dbpath (logger : ILogger) = 
        let log = logger.Log
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
                let header = dbHeaderPu.unpickle reader
                if header.XakeVer < XakeDbVersion then 
                    failwith "Database version is old."
                while stream.Position < stream.Length do
                    let targets = targetListPu.unpickle reader
                    let result = resultPU.unpickle reader
                    db := addResult !db targets result
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
    
type DatabaseApi<'result> = 
    | GetResult of Target * AsyncReplyChannel<'result option>
    | Store of Target list * 'result
    | Close
    | CloseWait of AsyncReplyChannel<unit>

/// Opens database.
let openDb resultPU dbPath (logger : ILogger) = 
    let db, dbWriter = impl.openDatabaseFile resultPU dbPath logger
    MailboxProcessor.Start(fun mbox -> 
        let rec loop (db) = 
            async { 
                let! msg = mbox.Receive()
                match msg with
                | GetResult(key, chan) -> 
                    db.Status
                    |> Map.tryFind key
                    |> chan.Reply
                    return! loop (db)
                | Store (targets, result) -> 
                    impl.targetListPu.pickle targets dbWriter
                    resultPU.pickle result dbWriter
                    return! loop (addResult db targets result)
                | Close -> 
                    logger.Log Info "Closing database"
                    dbWriter.Dispose()
                    return ()
                | CloseWait ch -> 
                    logger.Log Info "Closing database"
                    dbWriter.Dispose()
                    ch.Reply()
                    return ()
            }
        loop (!db))
