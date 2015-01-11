namespace Xake

[<AutoOpen>]
module DomainTypes =

  let private compareNames a b = System.String.Compare(a,b,true)

  type Artifact(name:string) =

    let fi = lazy (System.IO.FileInfo name)

    interface System.IComparable with
      member me.CompareTo other =
        match other with
        | :? Artifact as a -> compareNames me.Name a.Name
        | _ -> 1

    member this.Name = name
    member this.FullName = fi.Value.FullName
    member this.Exists =
        fi.Value.Refresh()
        fi.Value.Exists
    member this.LastWriteTime =
        fi.Value.Refresh()
        fi.Value.LastWriteTime

    member this.IsUndefined = System.String.IsNullOrWhiteSpace(name)
    static member Undefined = Artifact(null)

    override this.Equals(other) =
      match other with
        | :? Artifact as a -> 0 = compareNames this.Name a.Name
        | _ -> false
    override me.GetHashCode() =
        if me.IsUndefined then 0
        else name.ToLowerInvariant().GetHashCode()
    override me.ToString() = name
    // TODO refine our own type, keep paths relative

  type Target = FileTarget of Artifact | PhonyAction of string
  // TODO have no idea where to put this type and related methods (see fileset.fs) to

  let toArtifact name = Artifact name

  /// Defines common exception type
  exception XakeException of string

module Target =
  let getFullName = function
    | FileTarget f -> f.FullName
    | PhonyAction a -> a