namespace Research

module Lib1 =

    type Artifact = | File of string | Act of string
    type ArtifactMask = | FilePattern of string | TargetAct of string
    type Rule = Rule of ArtifactMask * (Artifact -> unit)

    type Rules = Rules of Rule list

    let makeFileRule pattern (action: string -> unit) = Rule ((FilePattern pattern), fun (File file) -> action file)

    let addRule rule (Rules rules) :Rules =
      rule :: rules |> Rules
    let setOptions (options:string) :Rules =
      Rules []

module variant1and2 =
  open Lib1
  let ( <* ) rules rule =
    addRule rule rules
  let ( *> ) pattern (action: string -> unit) =
    makeFileRule pattern action

  let abc =
    setOptions "aaa"
    <* "*.c" *>  fun x -> ()
    <* "*.cs" *> fun x -> ()
    <* makeFileRule "*.h" (fun x -> ())

module variant3 =
  open Lib1
  let ( *> ) pattern (action: string -> unit) = makeFileRule pattern action

  type RulesBuilder(options) =
    member this.Yield(()) = Rules[]
    [<CustomOperation("rule")>]   member this.Rule(rules, rule) = addRule rule rules

  let rules = new RulesBuilder()    
  
  let script = rules {
      // setOptions "aaa"
      rule (makeFileRule "*.c" (fun x -> ()))
      rule ("*.cs" *> fun x -> ())
      rule ("*.res" *> fun x -> ())
      }

module variant4 =
  open Lib1
  open Xake

  let ( *> ) pattern (action: string -> unit) =
    makeFileRule pattern action

  type XakeOptionsType = {
    /// Defines project root folder
    ProjectRoot : string
    /// Maximum number of threads to run the rules
    Threads: int

    /// Log file and verbosity level
    FileLog: string
    FileLogLevel: Level

    /// Console output verbosity level. Default is Warn
    ConLogLevel: Level
    /// Overrides "want", i.e. target list 
    WantOverride: string list
  }

  let XakeOptions = {
    ProjectRoot = ""
    Threads = 4
    ConLogLevel = Level.Warning

    FileLog = ""
    FileLogLevel = Level.Error
    WantOverride = []
    }

  type XakeScript = XakeScript of XakeOptionsType * Rule list

  type RulesBuilder(options) =
    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options,[])

    member this.Yield(()) = XakeScript (options,[])
    member this.Run(XakeScript (options,rules)) =
      printfn "running"
      printfn "Options: %A" options 
      printfn "Rules: %A" rules
      ()

    [<CustomOperation("rule")>]   member this.Rule(XakeScript (options,rules), rule) = XakeScript (options, rules @ [rule])
    [<CustomOperation("want")>]   member this.Want(XakeScript (options,rules), targets:string list) = XakeScript (options, rules)

  let xake options = new RulesBuilder(options)

  do xake {XakeOptions with FileLog = "build.log" } {

      want ["hello.exe"; "ppx.exe"]

      rule ("*.c" *> fun x -> ())
      rule ("*.cs" *> fun x -> ())
      rule ("*.res" *> fun x -> ())
      rule ("*.exe" *> fun x -> ())
      }

