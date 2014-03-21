[<AutoOpen>]
module Xake.Logging
// modified http://fssnip.net/8j

open System

/// Log levels.
type Level =
  | Error
  | Warning
  | Info
  | Debug
  | Verbose

let LevelToString level =
  match level with
    | Error -> "Err"
    | Warning -> "Warn"
    | Info -> "Info"
    | Debug -> "Debug"
    | Verbose -> "Verbose"
    | _ -> "Unknown"

let rec private logFilter = function
  | Error -> set [Error]
  | Warning -> set [Error; Warning]
  | Info -> set [Error; Warning; Info]
  | Debug -> set [Error; Warning; Info; Debug]
  | Verbose -> set [Error; Warning; Info; Debug; Verbose]
  
// defines output detail level
let private filterLevels = logFilter Info // Verbose

/// The inteface loggers need to implement.
type ILogger = abstract Log : Level -> Printf.StringFormat<'a,unit> -> 'a

/// Writes to console.
let ConsoleLogger = { 
  new ILogger with
    member __.Log level format =
      let write = 
        match Set.contains level filterLevels with
        | true -> sprintf "[%s] [%A] %s" (LevelToString level) System.DateTime.Now >> System.Console.WriteLine
        | false -> ignore
      Printf.kprintf write format
  }

/// Defines which logger to use.
let mutable DefaultLogger = ConsoleLogger

/// Logs a message with the specified logger.
let logUsing (logger: ILogger) = logger.Log

/// Logs a message using the default logger.
let log level message = logUsing DefaultLogger level message
let logInfo message = logUsing DefaultLogger Level.Info message
