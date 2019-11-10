namespace Xake

[<AutoOpen>]
module DomainTypes =

    let private stringCompare = if Env.isUnix then System.StringComparer.Ordinal else System.StringComparer.OrdinalIgnoreCase

    [<CustomEquality;CustomComparison>]
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
            
            override x.Equals(yobj) =
                match yobj with
                | :? Target as y -> stringCompare.Equals (x.FullName, y.FullName)
                | _ -> false

            override x.GetHashCode() = stringCompare.GetHashCode x.FullName
            interface System.IComparable with 
                member x.CompareTo y =
                    match y with
                    | :? Target as y -> stringCompare.Compare(x.FullName, y.FullName)
                    | _ -> invalidArg "y" "cannot compare target to different types"

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

    // expression type
    type Recipe<'a,'b> = Recipe of ('a -> Async<'a * 'b>)

    /// Data type for action's out parameter. Defined target file and named groups in pattern

    type 'ctx Rule =
        | FileRule of string * Recipe<'ctx,unit>
        | MultiFileRule of string list * Recipe<'ctx,unit>
        | PhonyRule of string * Recipe<'ctx,unit>
        | FileConditionRule of (string -> bool) * Recipe<'ctx,unit>
    type 'ctx Rules = Rules of 'ctx Rule list

    /// Defines common exception type
    exception XakeException of string

/// <summary>
/// A message to a progress reporter.
/// </summary>
type ProgressMessage =
    | Begin of System.TimeSpan
    | Progress of System.TimeSpan * int
    | End
