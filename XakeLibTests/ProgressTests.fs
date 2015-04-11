namespace XakeLibTests

open NUnit.Framework

type TaskDeps = string list
type Task = | Task of string * int * TaskDeps

type CpuState = | BusyUntil of int

type MachineState<'T when 'T:comparison> =
    { Cpu: CpuState list; Tasks: Map<'T,int>}

[<TestFixture>]
type ProgressTests() =

    let rec exec state task getDurationDeps =
        // Gets the thread that will be freed after specific moment
        let nearest after =
            let ready (BusyUntil x) = if x <= after then 0 else x in
            List.minBy ready

        // Updates the first item matching the criteria and returns the updated value
        let rec updateFirst predicate upd = function
            | [] -> None,[]
            | c::list when predicate c ->
                let updated = upd c in
                Some updated, updated :: list
            | c::list ->
                let result,list = (updateFirst predicate upd list) in
                result, c::list
        
        match state.Tasks |> Map.tryFind task with
        | Some result -> state,result
        | None ->
            let duration,deps = task |> getDurationDeps
            let readyAt (BusyUntil x)= x

            let mstate, endTime =
                match deps with
                | [] ->   state, 0
                | deps -> execMany state deps getDurationDeps
            let slot = mstate.Cpu |> nearest endTime
            let Some (BusyUntil result), newState =
                mstate.Cpu |> updateFirst ((=) slot) (readyAt >> max endTime >> (+) duration >> BusyUntil)
            {Cpu = newState; Tasks = mstate.Tasks |> Map.add task result}, result
//        |> fun r ->
//            printf "after %A" (task_name task)
//            printf "   %A\n\n" r
//            r

    // exec all deps, collect latest, allocate cpu
    and execMany state goals getDurationDeps =
        let machineState,endTime =
            goals |> List.fold (
                fun (prevState,prevTime) t ->
                    let newState,time = exec prevState t getDurationDeps in
                    (newState, max time prevTime)
                ) (state,0)

        machineState, endTime

    let estimate threadCount completed_tasks tasks goals =

        let getTaskName (Task (name,_,_)) = name

        let tasks_map = completed_tasks |> List.map (fun t -> (t, 0)) |> Map.ofList
        let machine_state = {Cpu = BusyUntil 0 |> List.replicate threadCount; Tasks = tasks_map}
        let taskMap = tasks |> List.map (fun task -> getTaskName task, task) |> Map.ofList

        let taskByName name = Map.find name taskMap
        let getDurationDeps (Task (_,duration,deps)) = duration,deps |> List.map taskByName
        let _,endTime = execMany machine_state (goals |> List.map taskByName) getDurationDeps
        in
        endTime

    [<TestCase(1, Result = 8)>]
    [<TestCase(6, Result = 8)>]
    member this.Test1(threads) =

        let tasks1 =
            [
                Task ("build", 1, ["link"])
                Task ("link", 2, ["compile"])
                Task ("compile", 5, [])
            ]

        estimate threads [] tasks1 ["build"]       

    [<TestCase(1, Result = 12)>]
    [<TestCase(2, Result = 10)>]
    member this.TestPara(threads) =

        let tasks1 =
            [
                Task ("build", 1, ["link1"; "link2"])
                Task ("link1", 2, ["compile"])
                Task ("link2", 2, ["compile"])
                Task ("compile", 7, [])
            ]

        estimate threads [] tasks1 ["build"]       

    [<TestCase(6, Result = 11)>]
    [<TestCase(1, Result = 21)>]
    member this.ComplexCase(threads) =
        let tasks1 =
            [
            Task ("build", 1, ["compile"])
            Task ("compile", 5,
                [
                    "version.h"
                    "commonheader.h"
                    "resources"
                    "resources-ru"
                ])
            Task ("version.h", 4, [])
            Task ("commonheader.h", 4, [])
            Task ("resources", 2, ["strings"])
            Task ("resources-ru", 3, ["strings"])
            Task ("strings", 2, [])
            ]
        estimate threads [] tasks1 ["build"]
