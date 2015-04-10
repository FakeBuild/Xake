namespace XakeLibTests

open NUnit.Framework

type Task =
    | Task of string * int
    | TaskWithDeps of string * int * string list

type CpuTime = CpuTime of int
type CpuState = | Free | BusyUntil of CpuTime

type MachineState<'T when 'T:comparison> =
    { Cpu: CpuState list; Tasks: Map<'T,CpuTime>}

[<TestFixture>]
type ProgressTests() =

    let rec exec state task getDuration getDeps =
        // Gets the time when thread will be available
        let readyAt = function
            | Free -> 0
            | BusyUntil (CpuTime x) -> x

        // Gets the thread that will be freed after specific moment
        let nearest after =
            let ready = function
                | Free -> 0
                | BusyUntil (CpuTime x) -> if x <= after then 0 else x
            List.minBy ready

        // Updates the first item matching the criteria and returns the updated value
        let rec updateFirst1 predicate upd = function
            | [] -> None,[]
            | c::list when predicate c ->
                let updated = upd c in
                Some updated, updated :: list
            | c::list ->
                let result,list = (updateFirst1 predicate upd list) in
                result, c::list
        
        match state.Tasks |> Map.tryFind task with
        | Some result -> state,result
        | None ->
            let mstate, CpuTime endTime =
                match getDeps task with
                | [] ->   state, CpuTime 0
                | deps -> execMany state deps getDuration getDeps
            let slot = mstate.Cpu |> nearest endTime
            let Some (BusyUntil result), newState =
                mstate.Cpu |> updateFirst1 ((=) slot) (readyAt >> (max endTime) >> ((+) (getDuration task)) >> CpuTime >> BusyUntil)
            {Cpu = newState; Tasks = mstate.Tasks |> Map.add task result}, result
//        |> fun r ->
//            printf "after %A" (task_name task)
//            printf "   %A\n\n" r
//            r

    // exec all deps, collect latest, allocate cpu
    and execMany state goals getDuration getDeps =
        let machineState,endTime =
            goals |> List.fold (
                fun (prevState,prevTime) t ->
                    let newState,CpuTime time = exec prevState t getDuration getDeps in
                    (newState, max time prevTime)
                ) (state,0)

        machineState, CpuTime endTime

    let estimate threadCount completed_tasks tasks goals =

        let getTaskName = function
            | Task (name,_) -> name
            | TaskWithDeps (name,_,_) -> name

        let getDuration = function
            | Task (_,d) -> d
            | TaskWithDeps (_,d,_) -> d

        let tasks_map = completed_tasks |> List.map (fun t -> (t, CpuTime 0)) |> Map.ofList
        let machine_state = {Cpu = (Free |> List.replicate threadCount); Tasks = tasks_map}
        let taskMap = tasks |> List.map (fun task -> getTaskName task, task) |> Map.ofList

        let taskByName name = Map.find name taskMap
        let getDeps = function |Task _ -> [] |TaskWithDeps(_,_,deps) -> deps |> List.map taskByName
        let _, CpuTime endTime = execMany machine_state (goals |> List.map taskByName) getDuration getDeps
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

        estimate threads [] tasks1 ["build"]       

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

        estimate threads [] tasks1 ["build"]       

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
        estimate threads [] tasks1 ["build"]
