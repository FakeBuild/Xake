namespace Xake

module BuildLog =

  open Xake
  open System

  let XakeVersion = "0.2-b"

  // structures, database processor and store
  type Timestamp = System.DateTime

  [<Measure>] type ms
  type StepInfo = StepInfo of string * int<ms>

  type Dependency =
    | File of Artifact * Timestamp
    | ArtifactDep of Target
    | EnvVar of string*string  // environment variable
    | Var of string*string     // any other data such as compiler version
    | AlwaysRerun              // indicates always rebuild the target
    | GetFiles of Fileset * Filelist

  type BuildResult = {
    Result: Target
    Built: Timestamp
    Depends: Dependency list
    Steps: StepInfo list
  }

  type DatabaseHeader = {
    XakeSign: string
    XakeVer: string
    ScriptDate: Timestamp
  }

  type Database = {
    Status: Map<Target,BuildResult>
  }

  (* API *)

  /// Creates a new build result
  let makeResult target =
    {Result = target; Built = DateTime.Now; Depends = []; Steps = []}

  /// Creates a new database
  let newDatabase() = {Database.Status = Map.empty}

  /// Adds result to a database
  let addResult db result = {db with Status = db.Status |> Map.add (result.Result) result}

type Agent<'t> = MailboxProcessor<'t>

module Storage =

  open BuildLog

  module private Persist = 

    open System
    // open System.IO
    open Pickler

    let artifact =
      wrap (toArtifact, fun a -> a.Name) str

    let target =
      alt
        (function | FileTarget _ -> 0 | PhonyAction _ -> 1)
        [|
          wrap (toArtifact >> FileTarget, fun (FileTarget f) -> f.Name) str
          wrap (PhonyAction, (fun (PhonyAction a) -> a)) str
        |]

    let step =
      wrap (
        (fun (n,d) -> StepInfo (n,d * 1<ms>)), fun (StepInfo (n,d)) -> (n,d/1<ms>))
        (pair str int)

    // Fileset of FilesetOptions * FilesetElement list
    
    let dependency =
      alt
        (function | ArtifactDep _ -> 0 | File _ -> 1 | EnvVar _ -> 2 |Var _ -> 3 |AlwaysRerun _ -> 4 |GetFiles _ -> 5)
        [|
          wrap (ArtifactDep, fun (ArtifactDep f) -> f) target
          wrap (File, fun (File (f,ts)) -> (f,ts)) (pair artifact date)
          wrap (EnvVar, fun (EnvVar (n,v)) -> n,v) (pair str str)
          wrap (Var, fun (Var (n,v)) -> n,v) (pair str str)
          wrap0 AlwaysRerun
          wrap (GetFiles, fun (GetFiles (fs,fi)) -> fs,fi) (pair filesetPickler filelistPickler)
        |]

    let result =
      wrap (
        (fun (r, built, deps, steps) -> {Result = r; Built = built; Depends = deps; Steps = steps}),
        fun r -> (r.Result, r.Built, r.Depends, r.Steps)
        )
        (quad target date (list dependency) (list step))
    let dbHeader =
      wrap (
        (fun (sign, ver, scriptDate) -> {DatabaseHeader.XakeSign = sign; XakeVer = ver; ScriptDate = scriptDate}),
        fun h -> (h.XakeSign, h.XakeVer, h.ScriptDate)
        )
        (triple str str date)

  type DatabaseApi =
    | GetResult of Target * AsyncReplyChannel<Option<BuildResult>>
    | Store of BuildResult
    | Close
    | CloseWait of AsyncReplyChannel<unit>

  let resultPU = Persist.result

  open System.IO

  let openDb path (logger:ILogger) = 
    let log = logger.Log
    let dbpath,bkpath = path </> ".xake", path </> ".xake" <.> "bak"

    let writeHeader w =
      let h = {DatabaseHeader.XakeSign = "XAKE"; XakeVer = XakeVersion; ScriptDate = System.DateTime.Now}
      Persist.dbHeader.pickle h w

    // if exists backup restore
    if File.Exists(bkpath) then
      log Level.Message "Backup file found ('%s'), restoring db" bkpath
      try File.Delete(dbpath) with _ -> ()
      File.Move (bkpath, dbpath)

    let db = ref (newDatabase())
    let fileRecords = ref 0

    // read database
    if File.Exists(dbpath) then
      try
        use reader = new BinaryReader (File.OpenRead(dbpath))
        let stream = reader.BaseStream

        let header = Persist.dbHeader.unpickle reader
        if header.XakeVer < XakeVersion then
          failwith "Database version is old. Recreating"
      
        while stream.Position < stream.Length do
          let result = resultPU.unpickle reader
          db := addResult !db result
          fileRecords := !fileRecords + 1

      // if fails create new
      with | ex ->
        log Level.Error "Failed to read database, so recreating. Got \"%s\"" <| ex.ToString()
        try File.Delete(dbpath) with _ -> ()

    // check if we can cleanup db
    if fileRecords.contents > db.contents.Status.Count * 5 then
      log Level.Message "Compacting database"
      File.Move(dbpath,bkpath)
      
      use writer = new BinaryWriter (File.Open(dbpath, FileMode.CreateNew))
      writeHeader writer
      db.contents.Status |> Map.toSeq |> Seq.map snd |> Seq.iter (fun r -> resultPU.pickle r writer)
      
      File.Delete(bkpath)

    let dbwriter = new BinaryWriter (File.Open(dbpath, FileMode.Append, FileAccess.Write))
    if dbwriter.BaseStream.Position = 0L then
      writeHeader dbwriter

    MailboxProcessor.Start(fun mbox ->
      let rec loop(db) = async {
        let! msg = mbox.Receive()
        match msg with
        | GetResult (key,chnl) ->
          db.Status |> Map.tryFind key |> chnl.Reply
          return! loop(db)
        | Store result ->
          resultPU.pickle result dbwriter
          return! loop(result |> addResult db)
        | Close ->
          log Info "Closing database"
          dbwriter.Dispose()
          return ()
        | CloseWait ch ->
          log Info "Closing database"
          dbwriter.Dispose()
          ch.Reply()
          return ()
      }
      loop(!db) )
