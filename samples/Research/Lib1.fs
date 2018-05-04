namespace Research

module Lib1 =

    type Level = |Error |Warning |Debug |Info 

    type Artifact = | File of string | Act of string
    type ArtifactMask = | FilePattern of string | TargetAct of string
    type Rule = Rule of ArtifactMask * (Artifact -> Async<unit>)

    type Rules = Rules of Rule list

    let makeFileRule pattern (action: string -> Async<unit>) = Rule ((FilePattern pattern), fun (File file) -> action file)

    let addRule rule (Rules rules) :Rules =
      rule :: rules |> Rules
    let setOptions (options:string) :Rules =
      Rules []

module variant1and2 =
  open Lib1
  let ( <* ) rules rule =
    addRule rule rules
  let ( *> ) pattern (action: string -> Async<unit>) =
    makeFileRule pattern action

  let abc =
    setOptions "aaa"
    <* "*.c" *>  (fun x -> async {()})
    <* "*.cs" *> (fun x -> async {()})
    <* makeFileRule "*.h" (fun x -> async {()})

module variant3 =
  open Lib1
  let ( *> ) pattern (action: string -> Async<unit>) = makeFileRule pattern action

  type RulesBuilder(options) =
    member this.Yield(()) = Rules[]
    [<CustomOperation("rule")>]   member this.Rule(rules, rule) = addRule rule rules
    [<CustomOperation("addrule")>]   member this.AddRule(rules, pattern, action) = rules |> addRule (makeFileRule pattern action)

  let rules = new RulesBuilder()    
  
  let script = rules {
      // setOptions "aaa"
      rule (makeFileRule "*.c" (fun x -> async{()}))
      rule ("*.cs" *> fun x -> async{()})
      //"*.cs" *> fun x -> async{()}
      addrule "*.res" (fun x -> async{()})
      }

module ``variant 4 - All together `` =
  open Lib1
  open System.IO

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

  module Core =
    type ExecState = ExecState of string list  
    let need (a:string list) = async {()}
    let exec (a:string list) = async {()}
    let run (a:string list) = async {()}

    // just an example of context type
    type ExecCtx = string list -> unit
    type Action<'a> = 'a -> Async<unit>
    type Rule = Rule of ArtifactMask * (Artifact -> Action<ExecCtx>)

  open Core
  type XakeScript = XakeScript of XakeOptionsType * Rule list * ExecState

  type RulesBuilder(options) =
    member o.Bind(x,f) = f x
    member o.Zero() = XakeScript (options,[], Core.ExecState [])

    member this.Yield(()) = XakeScript (options, [], Core.ExecState [])
    member this.Run(XakeScript (options,rules, exec)) =
      printfn "running"
      printfn "Options: %A" options 
      printfn "Rules: %A" rules
      ()

    [<CustomOperation("rule")>]   member this.Rule(XakeScript (options,rules,state), rule) = XakeScript (options, rules @ [rule], state)
    [<CustomOperation("want")>]   member this.Want(XakeScript (options,rules,state), targets:string list) = XakeScript (options, rules, state)

    member this.Need(XakeScript (options,rules,state), targets:string list) = async {()}

  let xake options = new RulesBuilder(options)

  let (-.) (file) newExt = Path.ChangeExtension( (new FileInfo(file)).FullName, newExt)

  let makeFileRule1 pattern (action: string -> Action<ExecCtx>) = Rule ((FilePattern pattern), fun (File file) -> action file)

  let ( *> ) pattern (action: string -> Action<ExecCtx>) = makeFileRule1 pattern action

  (* Action monad and expression builder *)
  
  type ActionBuilder() =
      let a = async
      member b.Zero()                   = fun (s:ExecCtx) -> a.Zero()
      member b.Delay(f)     = fun (s:ExecCtx) -> a.Delay(f)
      member b.Return(x)                = fun (s:ExecCtx) -> a.Return(x)
//      member b.ReturnFrom(x:Async<_>) = fun (s:ExecCtx) -> a.ReturnFrom(x)
      member b.Bind(p1:Action<ExecCtx>, f:(unit -> Async<unit>)):Action<ExecCtx>   = fun (s:ExecCtx) -> a.Bind(p1 s, f) |> Async.Ignore
//      member b.Combine(p1, p2)        = fun (s:ExecCtx) -> a.Combine(p1, p2)
      member b.For(e, prog)             = a.Bind(e, prog) |> Async.Ignore
            // TODO do For for composing rules
//      member b.Using(g, p)            = a.Using(g, p)
//      member b.While(gd, prog)        = a.While(gd, prog)
//      member b.TryFinally(p, cf)      = a.TryFinally(p, cf)
//      member b.TryWith(p, cf)         = a.TryWith(p, cf)

      // TODO fix/review me
      member this.Yield(()) = fun (s:ExecCtx) -> a.Zero()
      [<CustomOperation("need1")>]   member b.Need((s:Action<ExecCtx>), targets:string list) = a.Zero()

  let action = ActionBuilder()


  do xake {XakeOptions with FileLog = "build.log" } {
    
    // option0 globals (drawback: poorly composable scripts)
    // option1 alias on top of rules list
    // option2 pass exec state all around (to tasks and rules)
    // option3 need as declaration which is somehow is executed during run phase
    // option4 make xake a function with arguments: options, `body` function which has "want", "need" as arguments
    // option5 make xake script a function of class which provides implementation for "need"/"want"
    // (!) OPTION 6 make action monad on top of async. Pass exec context throughout    
    
    // let need a = async{()}

    want ["hello.exe"; "ppx.exe"]

    rule ("*.obj" *> fun (x) -> action {
      need1 [x -. "c"]
      do! Async.Sleep(100)
      })
    rule ("*.cs" *> fun x -> action{()})
    rule ("*.res" *> fun x -> action{()})
    rule ("*.exe" *> fun x -> action{()})
    }

module variant5 =
  open Lib1
  let ( *> ) pattern (action: string -> Async<unit>) = makeFileRule pattern action

  type RulesBuilder(options) =
    member this.Yield(()) = Rules[]
    member this.YieldFrom(x) = x
    member this.For(Rules s,f) = Rules (s @ [f])

    [<CustomOperation("rule")>]   member this.Rule(rules, rule) = addRule rule rules
    [<CustomOperation("rules")>]   member this.Rules(Rules rules, moreRules) = Rules (rules @ moreRules)

  let rules = new RulesBuilder()    
  
  let script = rules {
      // setOptions "aaa"
      //("*.cs" *> fun x -> ())

      rules [
        ("*.res" *> fun x -> ())
        ("*.res" *> fun x -> ())
      ]
      }