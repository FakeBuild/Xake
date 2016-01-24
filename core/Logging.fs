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
    | Message -> "Msg"
    | Error -> "Err"
    | Command -> "Cmd"
    | Warning -> "Warn"
    | Info -> "Info"
    | Debug -> "Debug"
    | Verbose -> "Verbose"
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

    type Message = Message of Level * string

    let defaultColor = Console.ForegroundColor

    let levelToColor = function
        | Level.Message -> ConsoleColor.White, ConsoleColor.White
        | Error   -> ConsoleColor.Red, ConsoleColor.DarkRed
        | Command -> ConsoleColor.Green, ConsoleColor.Green
        | Warning -> ConsoleColor.Yellow, ConsoleColor.DarkYellow
        | Info -> ConsoleColor.DarkGreen, defaultColor
        | Debug -> ConsoleColor.DarkGray, ConsoleColor.DarkGray
        | Verbose -> ConsoleColor.Gray, ConsoleColor.Gray
        | _ -> defaultColor, defaultColor


    let po = MailboxProcessor.Start(fun mbox -> 
        let rec loop () = 
            async { 
                let! (Message(level, text)) = mbox.Receive()

                let color, text_color = level |> levelToColor

                Console.ForegroundColor <- defaultColor
                Console.Write "["
                Console.ForegroundColor <- color
                Console.Write (LevelToString level)
                Console.ForegroundColor <- defaultColor
                Console.Write "] "

                Console.ForegroundColor <- text_color
                text |> System.Console.WriteLine
                Console.ForegroundColor <- defaultColor

                return! loop ()
            }
        loop ())


/// <summary>
/// Console logger.
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

let DumbConsoleLogger =
    ConsoleLoggerBase (
        fun level -> (LevelToString level) |> sprintf "[%s] %s" >> System.Console.WriteLine
        )

let ConsoleLogger =
    ConsoleLoggerBase (fun level s -> ConsoleSink.Message(level,s) |>  ConsoleSink.po.Post)

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
