namespace Xake

module internal ParseArgs = begin

    type 't ParseMode =
        | TopLevel
        | Number of string*('t -> int -> 't)
        | String of string*('t -> string -> 't)
        | KeyValue of string*('t -> string -> string -> 't)

    let get_args = System.Environment.GetCommandLineArgs >> List.ofArray

    let rec get_script_args = function
    | [] -> []
    | "--" :: rest -> rest
    | _ :: tail -> get_script_args tail

    let parseTopLevel arg optionsSoFar = 
        match arg with 

        // TODO support sequential/parallel runs e.g. "clean release-build;debug-build"

        | "-t" | "/t" -> 
            (optionsSoFar, Number ("thread count", fun o v -> {o with XakeOptionsType.Threads = v}))
        | "-R" | "/R" -> 
            (optionsSoFar, String ("root folder", fun o v -> {o with XakeOptionsType.ProjectRoot = v}))
        | "-FL" | "/FL" -> 
            (optionsSoFar, String ("file log filename", fun o v -> {o with XakeOptionsType.FileLog = v}))
        | "-D" | "/D" -> 
            (optionsSoFar, KeyValue ("variable", fun o k v -> {o with Vars = o.Vars @ [(k,v)] }))
        | "-LL" | "/LL" -> 
            (optionsSoFar, String ("console verbosity", fun o s -> {o with ConLogLevel = s |> parseVerbosity }))
        | "-FLL" | "/FLL" -> 
            (optionsSoFar, String ("filelog verbosity", fun o s -> {o with FileLogLevel = s |> parseVerbosity }))

        // handle unrecognized option
        | x when x.StartsWith("-") -> 
            printfn "Option '%s' is unrecognized" x
            (optionsSoFar, TopLevel)
        | x -> 
            ({optionsSoFar with Want = optionsSoFar.Want @ [x]}, TopLevel)

    let (|NumberVar|_|) str =
        match System.Int32.TryParse str with
        | (true, n) -> Some (NumberVar n)
        | _ -> None

    let readNumber name arg optionsSoFar fn = 
        match arg with
        | NumberVar v -> (fn optionsSoFar v, TopLevel)
        | _ -> 
            printfn "%s needs a second argument" name
            (optionsSoFar, TopLevel)

    let readString name arg optionsSoFar fn =
        (fn optionsSoFar arg, TopLevel)

    let readKeyValue name (arg:string) optionsSoFar fn =
        
        let k,v =
            match arg.IndexOfAny([|':'; '='|], 1) with
            | -1 -> arg,""
            | n -> arg.Substring(0, n), arg.Substring(n+1)

        (fn optionsSoFar k v, TopLevel)

    let foldFunction state element  = 
        match state with
        | (optionsSoFar, TopLevel) ->
            parseTopLevel element optionsSoFar

        | (optionsSoFar, Number (name, fn)) ->
            readNumber name element optionsSoFar fn

        | (optionsSoFar, String (name, fn)) ->
            readString name element optionsSoFar fn

        | (optionsSoFar, KeyValue (name, fn)) ->
            readKeyValue name element optionsSoFar fn

end

[<AutoOpen>]
module Main =

    open ParseArgs

    /// <summary>
    /// creates xake build script
    /// </summary>
    /// <param name="options"></param>
    let xake options =

        new RulesBuilder(options)

    /// <summary>
    /// Creates a script with script parameters passed as list of strings.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="initialOptions"></param>
    let xakeArgsStr args initialOptions =
        let options = args |> List.fold foldFunction (initialOptions, TopLevel) |> fst
        new RulesBuilder (options)

    /// <summary>
    /// Create xake build script using command-line arguments to define script options
    /// </summary>
    /// <param name="args"></param>
    /// <param name="options"></param>
    let xakeArgs =
        let args = get_args() |> get_script_args in
        xakeArgsStr args
