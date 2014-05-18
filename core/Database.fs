namespace Xake

module BuildLog =

  open Xake
  open System

  // structures, database processor and store
  type Key(target:Target) =

    let comparer = StringComparer.OrdinalIgnoreCase

    member this.Value = target
    member private this.FullName = target |> Target.getFullName

    override me.Equals other =
        match other with
        | :? Key as otherKey -> comparer.Equals(me.FullName, otherKey.FullName)
        | _ -> false

    override me.GetHashCode() = target.GetHashCode()

    interface IComparable with
      member me.CompareTo other =
        match other with
        | :? Key as otherKey -> comparer.Compare(me.FullName, otherKey.FullName)
        | _ -> 1

  type Timestamp = System.DateTime
  [<Measure>] type ms
  type StepInfo = StepInfo of string * int<ms>

  type Dependency =
    | File of Target    // file/target
    | EnvVar of string*string  // environment variable
    | Var of string*string     // any other data such as compiler version

  type BuildResult = {
    Result: Target
    Built: Timestamp
    Depends: Dependency list
    Steps: StepInfo list
  }

  type Database = {
    Status: Map<Key,BuildResult>
  }

  (* API *)

  /// Creates a new build result
  let makeResult target =
    {Result = target; Built = DateTime.Now; Depends = []; Steps = []}

  /// Creates a new database
  let newDatabase() = {Database.Status = Map.empty}

  /// Adds result to a database
  let addResult db result = {db with Status = db.Status |> Map.add (Key result.Result) result}

type Agent<'t> = MailboxProcessor<'t>

module Storage =

  open BuildLog

  module private Persist = 

    open Pickler
    open System
    open System.IO

    let targetPU =
      altPU
        (function | FileTarget _ -> 0 | PhonyAction _ -> 1)
        [|
          wrapPU ((fun f -> FileInfo f |> FileTarget), fun (FileTarget f) -> f.FullName) strPU
          wrapPU (PhonyAction, (fun (PhonyAction a) -> a)) strPU
        |]

    let stepPU =
      wrapPU (
        (fun (n,d) -> StepInfo (n,d * 1<ms>)), fun (StepInfo (n,d)) -> (n,d/1<ms>))
        (pairPU strPU intPU)

    let dependencyPU =
      altPU
        (function | File _ -> 0 | EnvVar _ -> 1 |Var _ -> 2)
        [|
          wrapPU (Dependency.File, fun (File f) -> f) targetPU
          wrapPU (EnvVar, fun (EnvVar (n,v)) -> n,v) (pairPU strPU strPU)
          wrapPU (Var, fun (Var (n,v)) -> n,v) (pairPU strPU strPU)
        |]

    let resultPU =
      wrapPU (
        (fun (r, built, deps, steps) -> {Result = r; Built = built; Depends = deps; Steps = steps}),
        fun r -> (r.Result, r.Built, r.Depends, r.Steps)
        )
        (quadPU targetPU datePU (listPU dependencyPU) (listPU stepPU))

  type DatabaseApi =
    | GetResult of Key * AsyncReplyChannel<Option<BuildResult>>
    | Store of BuildResult

  let createDb path (logger:ILogger) = 
    let log = logger.Log

    Agent.Start(fun mbox ->
    let rec loop(db) = async {
      let! msg = mbox.Receive()
      match msg with
      | GetResult (key,chnl) ->
        db.Status |> Map.tryFind key |> chnl.Reply
        return! loop(db)
      | Store result ->
        // TODO write to a file
        return! loop(result |> addResult db)
    }
    loop(newDatabase()) )
