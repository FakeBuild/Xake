module internal Xake.DependencyAnalysis

open Xake
open Database

/// <summary>
/// Dependency state.
/// </summary>
type ChangeReason =
    | NotChanged
    | Depends of Target
    | DependsMissingTarget of Target
    | FilesChanged of string list
    | Other of string

let TimeCompareToleranceMs = 10.0

/// <summary>
/// Gets target execution time in the last run
/// </summary>
/// <param name="ctx"></param>
/// <param name="target"></param>
let getExecTime ctx target =
    (fun ch -> GetResult(target, ch)) |> ctx.Db.PostAndReply
    |> Option.fold (fun _ r -> r.Steps |> List.sumBy (fun s -> s.OwnTime)) 0<ms>

/// Gets single dependency state and reason of a change.
let getDepState getVar getFileList (getChangedDeps: Target -> ChangeReason list) = function
    | FileDep (a:File, wrtime) when not((File.exists a) && abs((File.getLastWriteTime a - wrtime).TotalMilliseconds) < TimeCompareToleranceMs) ->
        let dbgInfo = File.exists a |> function
            | false -> "file does not exists"
            | _ -> sprintf "write time: %A vs %A" (File.getLastWriteTime a) wrtime
        FilesChanged [a.Name], Some dbgInfo

    | ArtifactDep (FileTarget file) when not (File.exists file) ->
        DependsMissingTarget (FileTarget file), None

    | ArtifactDep dependeeTarget ->
        dependeeTarget |> getChangedDeps |> List.filter ((<>) NotChanged)
        |> function
            |  [] -> NotChanged, None
            |item::_ ->
                Depends dependeeTarget, Some (sprintf "E.g. %A..." item)

    | EnvVar (name,value) when value <> Util.getEnvVar name ->
        Other <| sprintf "Environment variable %s was changed from '%A' to '%A'" name value (Util.getEnvVar name), None

    | Var (name,value) when value <> getVar name ->
        Other <| sprintf "Global script variable %s was changed '%A'->'%A'" name value (getVar name), None

    | AlwaysRerun ->
        Other <| "AlwaysRerun rule", Some "Rule indicating target has to be run regardless dependencies state"

    | GetFiles (fileset,files) ->
        let newfiles = getFileList fileset
        let diff = compareFileList files newfiles

        if List.isEmpty diff then
            NotChanged, None
        else
            Other <| sprintf "File list is changed for fileset %A" fileset, Some (sprintf "The diff list is %A" diff)
    | _ -> NotChanged, None


/// <summary>
/// Gets the list of reasons to rebuilt the target. Empty list means target is not changed.
/// </summary>
/// <param name="ctx"></param>
/// <param name="getTargetDeps">gets state for nested dependency</param>
/// <param name="target">The target to analyze</param>
let getChangeReasons ctx getTargetDeps target =

    // separates change reason into two lists and collabses FilesChanged all into one
    let collapseFilesChanged reasons =
        let files, other = reasons |> List.partition (fst >> function | ChangeReason.FilesChanged _ -> true | _ -> false)
        let filesChangedDbg = files |> List.collect (snd >> Option.toList)
        let filesChanged = files |> List.collect (fst >> fun (FilesChanged files | OtherwiseFail files) -> files) |> function | [] -> [] | ls -> [FilesChanged ls, Some (sprintf "%A" filesChangedDbg)]
        in
        filesChanged @ other |> List.rev


    let lastBuild = (fun ch -> GetResult(target, ch)) |> ctx.Db.PostAndReply

    match lastBuild with
    | Some {BuildResult.Depends = []} ->
        [Other "No dependencies", Some "It means target is not \"pure\" and depends on something beyond our control (oracle)"]

    | Some {BuildResult.Depends = depends; Targets = result} ->
        let depState = getDepState (Util.getVar ctx.Options) (toFileList ctx.Options.ProjectRoot) getTargetDeps

        depends
            |> List.map depState
            |> List.filter (fst >> (<>) ChangeReason.NotChanged)
            |> collapseFilesChanged
            |> function
            | [] ->
                match result with
                | targetList when targetList |> List.exists (function | FileTarget file when not (File.exists file) -> true | _ -> false) ->
                    [Other "target file does not exist", Some "The file has to be rebuilt regardless all its dependencies were not changed"]
                | _ -> []
            | ls -> ls

    | _ ->
        [Other "Not built yet", Some "Target was not built before or build results were cleaned so we don't know dependencies."]
    |> List.map fst

// gets task duration and list of targets it depends on. No clue why one method does both.
let getDurationDeps ctx getDeps t =
    match getDeps t with
    | [] -> 0<ms>, []
    | deps ->
        let targets = deps |> List.collect (function |Depends t |DependsMissingTarget t -> [t] | _ -> [])
        (getExecTime ctx t, targets)
//    |> fun (tt,dd) ->
//        printfn "For task %A duration:%A deps:%A" t tt dd
//        (tt,dd)

/// Dumps all dependencies for particular target
let dumpDeps (ctx: ExecContext) (target: Target list) =

    let getDeps = getChangeReasons ctx |> memoizeRec

    let doneTargets = System.Collections.Hashtable()
    let indent i = String.replicate i "  "

    let rec displayNestedDeps ii =
        function
        | ArtifactDep dependeeTarget ->
            printfn "%sArtifact: %A" (indent ii) (Target.fullName dependeeTarget)
            showTargetStatus (ii+1) dependeeTarget
        | _ -> ()
    and showDepStatus ii (d: Dependency) =
        match d with
        | AlwaysRerun ->
            printfn "%sAlways Rerun" (indent ii)
        | FileDep (a:File, wrtime) ->
            let changed = File.exists a |> function
                | true when abs((File.getLastWriteTime a - wrtime).TotalMilliseconds) >= TimeCompareToleranceMs ->
                    sprintf "CHANGED (%A <> %A)" wrtime (File.getLastWriteTime a)
                | false ->
                    "NOT EXISTS"
                | _ -> ""
            printfn "%sFile '%s' %A %s" (indent ii) a.Name wrtime changed

        | EnvVar (name,value) ->
            let newValue = Util.getEnvVar name
            let changed = if value <> newValue then sprintf "CHANGED %A => %A" value newValue else ""
            printfn "%sENV Var: '%s' = %A %s" (indent ii) name value changed

        | Var (name,value) ->
            printfn "%sScript var: '%s' = %A" (indent ii) name value

        | GetFiles (fileset, _) ->
            printfn "%sGetFiles: %A" (indent ii) fileset
        | _ ->
            ()
    and showTargetStatus ii (target: Target) =
        if not <| doneTargets.ContainsKey(target) then
            doneTargets.Add(target, 1)

            printfn "%sTarget %A" (indent ii) (Target.shortName target)

            let lastResult = (fun ch -> GetResult(target, ch)) |> ctx.Db.PostAndReply
            match lastResult with
            | Some {BuildResult.Depends = []} ->
                printfn "%sno dependencies" (indent ii)
            | Some {BuildResult.Depends = deps} ->
                deps |> List.iter (showDepStatus (ii+1))
                deps |> List.iter (displayNestedDeps (ii+1))
            | None ->
                printfn "%sno built yet (no stats)" (indent ii)

    target |> List.iter (showTargetStatus 0)