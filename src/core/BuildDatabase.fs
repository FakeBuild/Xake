module Xake.BuildDatabase

module Picklers =

    open Pickler
    open ExecTypes

    let target = Database.Picklers.targetPu

    let file = wrap (File.make, fun a ->  a.FullName) str

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

type DatabaseApi = Database.DatabaseApi<ExecTypes.BuildResult>

/// Opens the database
let openDb path loggers = Database.openDb Picklers.result path loggers