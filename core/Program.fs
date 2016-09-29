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

    let parseTopLevel (arg:string) optionsSoFar = 
        match arg.ToLowerInvariant() with 

        | "-h" | "/h" | "--help" | "/?" ->
            printf """
Usage:
 fsi <script file> [-- options target..]

Options:
  -h                  - displays help screen
  -t <task count>     - parallelize execution to <task count> processes. Defaults to CPU cores
  -r <root path>      - override the root path. Default is current directory
  -ll <log level>     - console log level (Silent | Quiet | Normal | Loud | Chatty | Diag)
  -fl <file log path> - specifies the name of the log file
  -fll <log level>    - specifies the logging level to a log file
  target1 .. targetN  - define the list of targets to be executed sequentially
  target1;target2;..targetN -- execute the targets simultaneously
  -d <name>=<value>   - defines a script variable value
  --dryrun            - defines a script variable value

            """
            exit(0)
        | "-t" | "/t" -> 
            (optionsSoFar, Number ("thread count", fun o v -> {o with ExecOptions.Threads = v}))
        | "-r" | "/r" -> 
            (optionsSoFar, String ("root folder", fun o v -> {o with ExecOptions.ProjectRoot = v}))
        | "-fl" | "/fl" -> 
            (optionsSoFar, String ("file log filename", fun o v -> {o with ExecOptions.FileLog = v}))
        | "-d" | "/d" -> 
            (optionsSoFar, KeyValue ("variable", fun o k v -> {o with Vars = o.Vars @ [(k,v)] }))
        | "-ll" | "/ll" -> 
            (optionsSoFar, String ("console verbosity", fun o s -> {o with ConLogLevel = s |> parseVerbosity }))
        | "-fll" | "/fll" -> 
            (optionsSoFar, String ("filelog verbosity", fun o s -> {o with FileLogLevel = s |> parseVerbosity }))
        | "-nologo" -> 
            ({optionsSoFar with Nologo = true}, TopLevel)
        | "--dryrun" | "--dry-run" -> 
            ({optionsSoFar with DryRun = true}, TopLevel)
        | "--dump" -> 
            ({optionsSoFar with DumpDeps = true}, TopLevel)

        | x when x.StartsWith("-") || x.StartsWith("/") ->
            printfn "Option '%s' is unrecognized" x
            (optionsSoFar, TopLevel)
        | x -> 
            ({optionsSoFar with Targets = optionsSoFar.Targets @ [x]}, TopLevel)

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

    let foldFunction state element =
        try
            match state with
            | (optionsSoFar, TopLevel) ->
                parseTopLevel element optionsSoFar

            | (optionsSoFar, Number (name, fn)) ->
                readNumber name element optionsSoFar fn

            | (optionsSoFar, String (name, fn)) ->
                readString name element optionsSoFar fn

            | (optionsSoFar, KeyValue (name, fn)) ->
                readKeyValue name element optionsSoFar fn
        with e ->
            let argName =
                match state with
                | (_, Number (name, _)) | (_, KeyValue (name, _)) | (_, String (name, _)) ->
                    name
                | _ ->
                    "switch"
            printfn "Failed to parse '%s' due to %s" argName e.Message
            (fst state, TopLevel)

end

[<AutoOpen>]
module Main =

    open ParseArgs

    /// <summary>
    /// Creates a script with script parameters passed as list of strings.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="initialOptions"></param>
    let xakeArgs args initialOptions =
        let options =
            if initialOptions.IgnoreCommandLine then initialOptions
            else args |> List.fold foldFunction (initialOptions, TopLevel) |> fst
        
        if not options.Nologo then
            printf "XAKE build tool %s\n\n" Xake.Const.Version

        new RulesBuilder (options)

    /// <summary>
    /// Create xake build script using command-line arguments to define script options
    /// </summary>
    /// <param name="options">Initial options set. Could be overridden by a command line arguments.
    /// Define option IgnoreCommandLine=true to ignore command line arguments
    /// </param>
    let xake options =
        let args = get_args() |> get_script_args in
        xakeArgs args options
