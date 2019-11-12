module ``Progress estimator``

open NUnit.Framework

open Xake
open Xake.Estimate

type TaskDeps = string list
type Task = | Task of string * int<Ms> * TaskDeps

let internal estimate threadCount completed_tasks tasks goals =

    let getTaskName (Task (name,_,_)) = name

    let tasks_map = completed_tasks |> List.map (fun t -> (t, 0<Ms>)) |> Map.ofList
    let machine_state = {Cpu = BusyUntil 0<Ms> |> List.replicate threadCount; Tasks = tasks_map}
    let taskMap = tasks |> List.map (fun task -> getTaskName task, task) |> Map.ofList

    let taskByName name = Map.find name taskMap
    let getDurationDeps (Task (_,duration,deps)) = duration,deps |> List.map taskByName
    let _,endTime = execMany machine_state getDurationDeps (goals |> List.map taskByName)
    in
    endTime

[<TestCase(1, ExpectedResult = 8<Ms>)>]
[<TestCase(6, ExpectedResult = 8<Ms>)>]
let Test1(threads) =

    let tasks1 =
        [
            Task ("build", 1<Ms>, ["link"])
            Task ("link", 2<Ms>, ["compile"])
            Task ("compile", 5<Ms>, [])
        ]

    estimate threads [] tasks1 ["build"]       

[<TestCase(1, ExpectedResult = 12<Ms>)>]
[<TestCase(2, ExpectedResult = 10<Ms>)>]
let TestPara(threads) =

    let tasks1 =
        [
            Task ("build", 1<Ms>, ["link1"; "link2"])
            Task ("link1", 2<Ms>, ["compile"])
            Task ("link2", 2<Ms>, ["compile"])
            Task ("compile", 7<Ms>, [])
        ]

    estimate threads [] tasks1 ["build"]       

[<TestCase(6, ExpectedResult = 11<Ms>)>]
[<TestCase(1, ExpectedResult = 21<Ms>)>]
let ComplexCase(threads) =
    let tasks1 =
        [
        Task ("build", 1<Ms>, ["compile"])
        Task ("compile", 5<Ms>,
            [
                "version.h"
                "commonheader.h"
                "resources"
                "resources-ru"
            ])
        Task ("version.h", 4<Ms>, [])
        Task ("commonheader.h", 4<Ms>, [])
        Task ("resources", 2<Ms>, ["strings"])
        Task ("resources-ru", 3<Ms>, ["strings"])
        Task ("strings", 2<Ms>, [])
        ]
    estimate threads [] tasks1 ["build"]

[<TestCase(1, ExpectedResult = 9<Ms>)>]
[<TestCase(2, ExpectedResult = 5<Ms>)>]
let TestPara2(threads) =

    let tasks1 =
        [
            Task ("main", 0<Ms>, ["t1"; "t2"])
            Task ("t1", 4<Ms>, [])
            Task ("t2", 5<Ms>, [])
        ]

    estimate threads [] tasks1 ["main"]       
