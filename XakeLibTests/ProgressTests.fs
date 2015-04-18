namespace XakeLibTests

open NUnit.Framework
open Xake.Progress.Estimate

type TaskDeps = string list
type Task = | Task of string * int * TaskDeps

[<TestFixture>]
type ProgressTests() =

    let estimate threadCount completed_tasks tasks goals =

        let getTaskName (Task (name,_,_)) = name

        let tasks_map = completed_tasks |> List.map (fun t -> (t, 0)) |> Map.ofList
        let machine_state = {Cpu = BusyUntil 0 |> List.replicate threadCount; Tasks = tasks_map}
        let taskMap = tasks |> List.map (fun task -> getTaskName task, task) |> Map.ofList

        let taskByName name = Map.find name taskMap
        let getDurationDeps (Task (_,duration,deps)) = duration,deps |> List.map taskByName
        let _,endTime = execMany machine_state getDurationDeps (goals |> List.map taskByName)
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

    [<TestCase(1, Result = 9)>]
    [<TestCase(2, Result = 5)>]
    member this.TestPara2(threads) =

        let tasks1 =
            [
                Task ("main", 0, ["t1"; "t2"])
                Task ("t1", 4, [])
                Task ("t2", 5, [])
            ]

        estimate threads [] tasks1 ["main"]       

