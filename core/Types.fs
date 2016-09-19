namespace Xake

[<AutoOpen>]
module DomainTypes =

    type Target =
        | FileTarget of File
        | PhonyAction of string

        with
            member internal this.ShortName =
                match this with
                | FileTarget file -> file.Name
                | PhonyAction name -> name
            member internal this.FullName =
                match this with
                | FileTarget file -> file.FullName
                | PhonyAction name -> name

    // structures, database processor and store
    type Timestamp = System.DateTime

    [<Measure>]
    type ms

    type Dependency =
        | FileDep of File * Timestamp // regular file (such as source code file), triggers when file date/time is changed
        | ArtifactDep of Target // other target (triggers when target is rebuilt)
        | EnvVar of string * string option // environment variable
        | Var of string * string option // any other data such as compiler version (not used yet)
        | AlwaysRerun // trigger always
        | GetFiles of Fileset * Filelist // depends on set of files. Triggers when resulting filelist is changed

    type StepInfo =
        { Name: string; Start: System.DateTime; OwnTime: int<ms>; WaitTime: int<ms> }
        with static member Empty = {Name = ""; Start = new System.DateTime(1900,1,1); OwnTime = 0<ms>; WaitTime = 0<ms>}

    type BuildResult =
        { Result : Target
          Built : Timestamp
          Depends : Dependency list
          Steps : StepInfo list }

    // expression type
    type Action<'a,'b> = Action of (BuildResult * 'a -> Async<BuildResult * 'b>)

    /// Data type for action's out parameter. Defined target file and named groups in pattern
    type RuleActionArgs = RuleActionArgs of File * Map<string,string>

    type 'ctx Rule =
        | FileRule of string * (RuleActionArgs -> Action<'ctx,unit>)
        | PhonyRule of string * Action<'ctx,unit>
        | FileConditionRule of (string -> bool) * (RuleActionArgs -> Action<'ctx,unit>)
    type 'ctx Rules = Rules of 'ctx Rule list

    type RuleActionArgs with
        /// Gets the resulting file.
        member this.File = let (RuleActionArgs (file,_)) = this in file
        /// Gets the full name of resulting file.
        member this.FullName = let (RuleActionArgs (file,_)) = this in File.getFullName file

        /// Gets group (part of the name) by its name.
        member this.GetGroup(key) =
            let (RuleActionArgs (_,groups)) = this in
            groups |> Map.tryFind key |> function |Some v -> v | None -> ""

    /// Defines common exception type
    exception XakeException of string

/// Contains a methods for accessing RuleActionArgs members.
module RuleArgs =

    let getFile (args:RuleActionArgs) = args.File
    let getFullName (RuleActionArgs (file,_)) = File.getFullName file

    /// Gets all matched groups.
    let getGroups (RuleActionArgs (_,groups)) = groups

    /// Gets group (part of the name) by its name.
    let getGroup key (args:RuleActionArgs) = args.GetGroup key

/// <summary>
/// A message to a progress reporter.
/// </summary>
type ProgressMessage =
    | Begin of System.TimeSpan
    | Progress of System.TimeSpan * int
    | End
