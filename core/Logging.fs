[<AutoOpen>]
module Xake.Logging
// modified http://fssnip.net/8j

open System

/// Log levels.
type Level =
  | Message
  | Error
  | Command
  | Warning
  | Info
  | Debug
  | Verbose

type Verbosity =
  | Silent
  | Quiet
  | Normal
  | Loud
  | Chatty
  | Diag

let LevelToString = function
  | Message -> "Msg"
  | Error -> "Err"
  | Command -> "Cmd"
  | Warning -> "Warn"
  | Info -> "Info"
  | Debug -> "Debug"
  | Verbose -> "Verbose"

let private logFilter = function
  | Silent -> set []
  | Quiet -> set [Message; Error]
  | Normal -> set [Message; Error; Command]
  | Loud -> set [Message; Error; Command; Warning]
  | Chatty -> set [Message; Error; Command; Warning; Info]
  | Diag -> set [Message; Error; Command; Warning; Info; Debug; Verbose]
  
// defines output detail level
let private filterLevels = logFilter Chatty

/// The inteface loggers need to implement.
type ILogger = abstract Log : Level -> Printf.StringFormat<'a,unit> -> 'a

let createFileLogger fileName = MailboxProcessor.Start(fun mbox ->
  let rec loop() = async {
    let! msg = mbox.Receive()
    System.IO.File.AppendAllLines(fileName, [msg])

    return! loop()
  }
  loop() )

let CustomLogger filter writeFn =
  {
  new ILogger with
    member __.Log level format =
      let write = 
        if filter level then
          sprintf "[%s] %s" (LevelToString level) >> writeFn
        else
          ignore
      Printf.kprintf write format
  }

/// Writes to file.
let FileLogger name maxLevel =
  let filterLevels = logFilter maxLevel
  System.IO.File.Delete name
  let logger = createFileLogger name
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
