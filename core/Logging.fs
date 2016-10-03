[<AutoOpen>]
module Xake.Logging

/// <summary>
/// Log levels.
/// </summary>
type Level = 
    | Message
    | Error
    | Command
    | Warning
    | Info
    | Debug
    | Verbose
    | Never

/// <summary>
/// Output verbosity level.
/// </summary>
type Verbosity = 
    | Silent
    | Quiet
    | Normal
    | Loud
    | Chatty
    | Diag

let LevelToString = 
    function 
    | Message -> "MSG"
    | Error -> "ERROR"
    | Command -> "CMD"
    | Warning -> "WARN"
    | Info -> "INF"
    | Debug -> "DBG"
    | Verbose -> "TRACE"
    | _ -> ""

let private logFilter = 
    function 
    | Silent -> set []
    | Quiet -> set [ Message; Error ]
    | Normal -> set [ Message; Error; Command ]
    | Loud -> set [ Message; Error; Command; Warning ]
    | Chatty -> set [ Message; Error; Command; Warning; Info ]
    | Diag -> set [ Message; Error; Command; Warning; Info; Debug; Verbose ]

/// <summary>
/// The inteface loggers need to implement.
/// </summary>
type ILogger = 
    abstract Log : Level -> Printf.StringFormat<'a, unit> -> 'a

let private createFileLogger fileName = 
    MailboxProcessor.Start(fun mbox -> 
        let rec loop() = 
            async { 
                let! msg = mbox.Receive()
                System.IO.File.AppendAllLines(fileName, [ msg ])
                return! loop()
            }
        loop())

/// <summary>
/// Creates a custom logger.
/// </summary>
/// <param name="filter">The filter to apply to messages</param>
/// <param name="writeFn">The function that dumps a message</param>
let CustomLogger filter writeFn = 
    { new ILogger with
          member __.Log level format = 
              let write = 
                  if filter level then 
                      sprintf "[%s] %s" (LevelToString level) >> writeFn
                  else ignore
              Printf.kprintf write format }

/// <summary>
/// A logger that writes to file.
/// </summary>
/// <param name="name"></param>
/// <param name="maxLevel"></param>
let FileLogger name maxLevel = 
    let filterLevels = logFilter maxLevel
    System.IO.File.Delete name
    let logger = createFileLogger name
    { new ILogger with
          member __.Log level format = 
              let write = 
                  match Set.contains level filterLevels with
                  | true -> 
                      sprintf "[%s] [%A] %s" (LevelToString level) 
                          System.DateTime.Now >> logger.Post
                  | false -> ignore
              Printf.kprintf write format }

module private ConsoleSink =

    open System

    // Going to render progress bar this way:
    // | 22% [MM------------------] 0m 12s left
    let ProgressBarLen = 20
    type Message = | Message of Level * string | Progress of int * System.TimeSpan | Flush of AsyncReplyChannel<unit>

    let levelToColor = function
        | Level.Message -> Some (ConsoleColor.White, ConsoleColor.White)
        | Command -> Some (ConsoleColor.White, ConsoleColor.Gray)
        | Error   -> Some (ConsoleColor.Red, ConsoleColor.DarkRed)
        | Debug -> Some (ConsoleColor.Green, ConsoleColor.DarkGreen)
        | Warning -> Some (ConsoleColor.Yellow, ConsoleColor.DarkYellow)
        | Info    -> Some (ConsoleColor.Cyan, ConsoleColor.DarkCyan)
        | Verbose -> Some (ConsoleColor.Magenta, ConsoleColor.DarkMagenta)
        | _ -> None


    let po = MailboxProcessor.Start(fun mbox -> 
        let fmtTs (ts:System.TimeSpan) =
            (if ts.TotalHours >= 1.0 then "h'h'\ mm'm'\ ss's'"
            else if ts.TotalMinutes >= 1.0 then "mm'm'\ ss's'"
            else "'0m 'ss's'")
            |> ts.ToString
        let rec loop (progressMessage) =
            let wipe pos =
                match progressMessage with
                | None -> ()
                | Some x ->
                    let extraChars = (String.length x) - pos
                    System.Console.Write (if extraChars > 0 then (String.replicate extraChars " ") else "")
            let renderProgress = function
                | Some (outputString: string) ->
                    Console.ForegroundColor <- ConsoleColor.White
                    Console.Write outputString
                    wipe outputString.Length

                    Console.ResetColor()
                | None -> ()

            async { 
                let! msg = mbox.Receive()
                match msg with
                | Message(level, text) ->
                    match level |> levelToColor with
                    | Some (color, text_color) ->
                        let levelStr = sprintf "[%s] " (LevelToString level)

                        Console.ForegroundColor <- color
                        Console.Write levelStr

                        Console.ForegroundColor <- text_color
                        text |> System.Console.Write

                        wipe (levelStr.Length + text.Length)
                        System.Console.WriteLine ""
                        renderProgress progressMessage

                    | _ -> ()
                    Console.ResetColor()

                | Progress (pct, timeLeft) ->
                    let outputString =
                        match pct with
                        | a when a >= 100 || a < 0 || timeLeft.TotalMilliseconds < 100.0 -> ""
                        | pct ->
                            let barlen = pct * ProgressBarLen / 100
                            sprintf "%3d%% [%s%s] %s\r" pct (String.replicate barlen "=") (String.replicate (ProgressBarLen - barlen) "-") (fmtTs timeLeft)

                    renderProgress (Some outputString)
                    return! loop (Some outputString)

                | Flush ch ->
                    wipe 0
                    Console.Write "\r"
                    ch.Reply ()
                    return! loop None

                return! loop progressMessage
            }
        loop None)


/// <summary>
/// Base console logger.
/// </summary>
/// <param name="maxLevel"></param>
let private ConsoleLoggerBase (write: Level -> string -> unit) maxLevel = 
    let filterLevels = logFilter maxLevel
    { new ILogger with
          member __.Log level format = 
              let write = 
                  match filterLevels |> Set.contains level with
                  | true -> write level
                  | false -> ignore
              Printf.kprintf write format }

/// Simplistic console logger.
let DumbConsoleLogger =
    ConsoleLoggerBase (
        fun level -> (LevelToString level) |> sprintf "[%s] %s" >> System.Console.WriteLine
        )

/// Console logger with colors highlighting
let ConsoleLogger =
    ConsoleLoggerBase (fun level s -> ConsoleSink.Message(level,s) |>  ConsoleSink.po.Post)

/// Ensures all logs finished pending output.
let FlushLogs () =
    ConsoleSink.po.PostAndReply (ConsoleSink.Flush, 100) |> ignore

/// Draws a progress bar to console log.
let WriteConsoleProgress =
    let swap (a,b) = (b,a) in
    swap >> ConsoleSink.Progress >> ConsoleSink.po.Post

/// <summary>
/// Creates a logger that is combination of two loggers.
/// </summary>
/// <param name="log1"></param>
/// <param name="log2"></param>
let CombineLogger (log1 : ILogger) (log2 : ILogger) = 
    { new ILogger with
          member __.Log level (fmt : Printf.StringFormat<'a, unit>) : 'a = 
              let write s = 
                  log1.Log level "%s" s
                  log2.Log level "%s" s
              Printf.kprintf write fmt }

/// <summary>
/// A logger decorator that adds specific prefix to a message.
/// </summary>
/// <param name="prefix"></param>
/// <param name="log"></param>
let PrefixLogger (prefix:string) (log : ILogger) = 
    { new ILogger with
          member __.Log level format = 
              let write = sprintf "%s%s" prefix >> log.Log level "%s"
              Printf.kprintf write format }

/// <summary>
/// Parses the string value into verbosity level.
/// </summary>
/// <param name="parseVerbosity"></param>
let parseVerbosity = function
    | "Silent" -> Verbosity.Silent
    | "Quiet" -> Verbosity.Quiet
    | "Normal" -> Verbosity.Normal
    | "Loud" -> Verbosity.Loud
    | "Chatty" -> Verbosity.Chatty
    | "Diag" -> Verbosity.Diag
    | s ->
        failwithf "invalid verbosity: %s. Expected one of %s" s "Silent | Quiet | Normal | Loud | Chatty | Diag"
