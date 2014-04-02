[<AutoOpen>]
module Xake.Logging
// modified http://fssnip.net/8j

open System

/// Log levels.
type Level =
  // | Quiet
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

let rec private logFilter = function
  | Error -> set [Error]
  | Warning -> set [Error; Warning]
  | Info -> set [Error; Warning; Info]
  | Debug -> set [Error; Warning; Info; Debug]
  | Verbose -> set [Error; Warning; Info; Debug; Verbose]
  
// defines output detail level
let private filterLevels = logFilter Info

/// The inteface loggers need to implement.
type ILogger = abstract Log : Level -> Printf.StringFormat<'a,unit> -> 'a

let createFileLogger fileName = MailboxProcessor.Start(fun mbox ->
  let rec loop() = async {
    let! msg = mbox.Receive()
    System.IO.File.AppendAllLines(fileName, [msg])

    return! loop()
  }
  loop() )

/// Writes to file.
let FileLogger name maxLevel =
  let filterLevels = logFilter maxLevel
  System.IO.File.Delete "build.log"
  let logger = createFileLogger "build.log"
  { 
  new ILogger with
    member __.Log level format =
      let write = 
        match Set.contains level filterLevels with
        | true -> sprintf "[%s] [%A] %s" (LevelToString level) System.DateTime.Now >> logger.Post
        | false -> ignore
      Printf.kprintf write format
  }

/// Writes to console.
let ConsoleLogger maxLevel =
  let filterLevels = logFilter maxLevel
  {
  new ILogger with
    member __.Log level format =
      let write = 
        match Set.contains level filterLevels with
        | true -> sprintf "[%s] %s" (LevelToString level) >> System.Console.WriteLine
        // | true -> sprintf "[%s] [%A] %s" (LevelToString level) System.DateTime.Now >> System.Console.WriteLine
        | false -> ignore
      Printf.kprintf write format
  }

let CombineLogger (log1:ILogger) (log2:ILogger) =
  { 
  new ILogger with
    member __.Log level (fmt:Printf.StringFormat<'a,unit>) : 'a =
      let write s =
        log1.Log level "%s" s
        log2.Log level "%s" s

      Printf.kprintf write fmt
  }

/// Defines which logger to use.
let mutable DefaultLogger =
  CombineLogger (ConsoleLogger Level.Info) (FileLogger "build.log" Level.Verbose)

/// Logs a message with the specified logger.
let logUsing (logger: ILogger) = logger.Log

/// Logs a message using the default logger.
let log level message = logUsing DefaultLogger level message
let logInfo message = logUsing DefaultLogger Level.Info message
