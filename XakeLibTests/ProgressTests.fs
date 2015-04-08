namespace XakeLibTests

open NUnit.Framework

type Task =
    | Task of string * int
    | TaskWithDeps of string * int * string list

type CpuTime = CpuTime of int
type CpuState = | Free | BusyUntil of CpuTime

type TaskState =
    | TaskCompleted of CpuTime
    | NotRun

//type TaskStates = Map<Task,TaskState>

type MachineState = | MachineState of CpuState list * Map<string,TaskState>

[<TestFixture>]
type ProgressTests() =

    /// Gets task duration
    let duration = function
        | Task (_,d) -> d
        | TaskWithDeps (_,d,_) -> d

    let task_name = function
        | Task (name,_)
        | TaskWithDeps (name,_,_) -> name
    
    /// Gets the time when thread will be available
    let readyAt = function
        | Free -> 0
        | BusyUntil (CpuTime x) -> x

    /// Updates the first item matching the criteria and returns the updated value
    let rec updateFirst1 predicate upd = function
        | [] -> None,[]
        | c::list when predicate c ->
            let updated = upd c in
            Some updated, updated :: list
        | c::list ->
            let result,list = (updateFirst1 predicate upd list) in
            result, c::list

    /// Gets the thread that will be freed after specific moment
    let nearest after =
        let ready = function
            | Free -> 0
            | BusyUntil (CpuTime x) -> if x <= after then 0 else x
        List.minBy ready

    let rec exec state task getTask =
        let name = task_name task
        let (MachineState (_,tasks)) = state

        match tasks |> Map.tryFind name with
        | Some result -> state,result
        | None ->
            let MachineState(newState,tasks), CpuTime endTime =
                match task with
                | Task _ ->              state, CpuTime 0
                | TaskWithDeps (_,_,deps) -> execMany state deps getTask
            let slot = newState |> nearest endTime
            let Some (BusyUntil result),newState =
                newState |> updateFirst1 ((=) slot) (readyAt >> (max endTime) >> ((+) (duration task)) >> CpuTime >> BusyUntil)
            let newTasks = tasks |> Map.add name (TaskCompleted result)
            MachineState (newState, newTasks), TaskCompleted result
//        |> fun r ->
//            printf "after %A" (task_name task)
//            printf "   %A\n\n" r
//            r

    // exec all deps, collect latest, allocate cpu
    and execMany state goals getTask =
        let machineState,endTime =
            goals |> List.map getTask |> List.fold (
                fun (prevState,prevTime) t ->
                    let newState,(TaskCompleted (CpuTime time)) = exec prevState t getTask in
                    (newState, max time prevTime)
                ) (state,0)

        machineState, CpuTime endTime

    let estimate threads tasks goals =

        let pool = MachineState ((List.replicate threads Free), Map.empty)
        let taskMap = tasks |> List.map (fun task -> task_name task, task) |> Map.ofList

        let _, CpuTime endTime = execMany pool goals (fun name -> Map.find name taskMap)
        in
        endTime

    [<TestCase(1, Result = 8)>]
    [<TestCase(6, Result = 8)>]
    member this.Test1(threads) =

        let tasks1 =
            [
                TaskWithDeps ("build", 1, ["link"])
                TaskWithDeps ("link", 2, ["compile"])
                Task ("compile", 5)
            ]

        estimate threads tasks1 ["build"]       

    [<TestCase(1, Result = 12)>]
    [<TestCase(2, Result = 10)>]
    member this.TestPara(threads) =

        let tasks1 =
            [
                TaskWithDeps ("build", 1, ["link1"; "link2"])
                TaskWithDeps ("link1", 2, ["compile"])
                TaskWithDeps ("link2", 2, ["compile"])
                Task ("compile", 7)
            ]

        estimate threads tasks1 ["build"]       

    [<TestCase(6, Result = 11)>]
    [<TestCase(1, Result = 11)>]
    member this.ComplexCase(threads) =
        let tasks1 =
            [
            TaskWithDeps ("build", 1, ["compile"])
            TaskWithDeps ("compile", 5,
                [
                    "version.h"
                    "commonheader.h"
                    "resources"
                    "resources-ru"
                ])
            Task ("version.h", 4)
            Task ("commonheader.h", 4)
            TaskWithDeps ("resources", 2, ["strings"])
            TaskWithDeps ("resources-ru", 3, ["strings"])
            Task ("strings", 2)
            ]
        estimate threads tasks1 ["build"]
