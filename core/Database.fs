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

  module private PersistBin = 

    open System
    open System.IO

    type OutState = System.IO.BinaryWriter
    type InState = System.IO.BinaryReader
    type Pickler<'T> = 'T -> OutState -> unit
    type Unpickler<'T> = InState -> 'T

    let byteP (b : byte) (st : OutState) = st.Write(b)
    let byteU (st : InState) = st.ReadByte()

    let boolP b st = byteP (if b then 1uy else 0uy) st
    let boolU st = let b = byteU st in (b = 1uy)

    let intP (i : Int32) (st : OutState) = st.Write(i)
    let intU (st : InState) = st.ReadInt32()

    let int64P (i : Int64) (st : OutState) = st.Write(i)
    let int64U (st : InState) = st.ReadInt64()

    let strP (s : string) (st : OutState) = st.Write(s)
    let strU (st : InState) = st.ReadString()

    let dtP (d : DateTime) = int64P d.Ticks
    let dtU st : DateTime = st |> (int64U >> DateTime.FromBinary)

    let tup2P p1 p2 (a, b) (st : OutState) =       (p1 a st : unit); (p2 b st : unit)
    let tup3P p1 p2 p3 (a, b, c) (st : OutState) = do p1 a st; do p2 b st; do p3 c st
    let tup4P p1 p2 p3 p4 (a, b, c, d) (st : OutState) = do p1 a st; do p2 b st; do p3 c st; do p4 d st

    let tup2U p1 p2 (st : InState) = p1 st, p2 st
    let tup3U p1 p2 p3 (st : InState) = p1 st, p2 st, p3 st
    let tup4U p1 p2 p3 p4 (st : InState) = p1 st, p2 st, p3 st, p4 st

    /// Outputs a list into the given output stream by pickling each element via f.
    /// A zero indicates the end of a list, a 1 indicates another element of a list.
    let rec listP f lst st =
      match lst with
      | [] -> byteP 0uy st
      | h :: t -> byteP 1uy st; f h st; listP f t st
    // Reads a list from a given input stream by unpickling each element via f.
    let listU f st =
      let rec loop acc =
        let tag = byteU st
        match tag with
        | 0uy -> List.rev acc
        | 1uy -> let a = f st in loop (a :: acc)
        | n -> failwithf "listU: found number %d" n
      loop []

    let targetP = function
      | FileTarget f ->  tup2P byteP strP (1uy, f.FullName)
      | PhonyAction a -> tup2P byteP strP (2uy, a)

    let targetU st =
      match tup2U byteU strU st with
      | (1uy,fullname) -> FileTarget <| FileInfo fullname
      | (2uy,name) -> PhonyAction name
      | _ -> failwith "Not a target"

    let stepP (StepInfo (n,d)) = tup2P strP intP (n,d/1<ms>)
    let stepU:InState -> StepInfo = tup2U strU intU >> fun (n,d) -> StepInfo (n,d*1<ms>)

    let dependencyP = function
      | File f   -> tup2P byteP targetP (1uy, f)
      | EnvVar (name,value) -> tup3P byteP strP strP (2uy, name, value)
      | Var (name,value) -> tup3P byteP strP strP (3uy, name, value)

    let dependencyU st =
      match byteU st with
      | 1uy -> Dependency.File <| targetU st
      | 2uy -> EnvVar <| tup2U strU strU st
      | 3uy -> Var <| tup2U strU strU st
      | _ -> failwith "Not a dependency"

    let resultP (r:BuildResult) =
      tup4P targetP dtP (listP dependencyP) (listP stepP) (r.Result, r.Built, r.Depends, r.Steps)

    let resultU st =
      let (r, built, deps, steps) = tup4U targetU dtU (listU dependencyU) (listU stepU) st in
      {Result = r; Built = built; Depends = deps; Steps = steps}

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
