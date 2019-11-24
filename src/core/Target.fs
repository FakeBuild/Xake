[<AutoOpen>]
module Xake.Target

let private stringCompare = if Env.isUnix then System.StringComparer.Ordinal else System.StringComparer.OrdinalIgnoreCase

[<CustomEquality;CustomComparison>]
type Target =
    | FileTarget of Xake.File
    | PhonyAction of string

with
    member private this.FullName =
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Target =

    /// Gets short (user friendly) target name
    let shortName = function
        | FileTarget file -> file.Name
        | PhonyAction name -> name

    // Get fully qualifying target name
    let fullName = function
        | FileTarget file -> file.FullName
        | PhonyAction name -> name
