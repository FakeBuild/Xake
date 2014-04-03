namespace Xake

[<AutoOpen>]
module DomainTypes =

  open System.IO

  type Artifact = FileInfo 
  type BuildAction = BuildAction of (Artifact -> Async<unit>)

  type Rules = Rules of string list
  let addRule rule (Rules rules) :Rules =
    rule :: rules |> Rules
  let setOptions (options:string) :Rules =
    Rules []

  let abc =
    setOptions "aaa"
    |> addRule "abc"
    |> addRule "def"
    |> addRule "fgh"
