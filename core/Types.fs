namespace Xake

[<AutoOpen>]
module DomainTypes = 
    let private compareNames a b = System.String.Compare(a, b, true)
    
    type Artifact(name : string) = 
        let fi = lazy (System.IO.FileInfo name)
    
        // TODO refine our own type, keep paths relative
            
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

    type Target = 
        | FileTarget of Artifact
        | PhonyAction of string
    
    // structures, database processor and store
    type Timestamp = System.DateTime
    
    [<Measure>]
    type ms
    
    type StepInfo = 
        /// name, start time, total duration, wait time
        // | StepInfo of string * System.DateTime * int<ms> * int<ms>
        {Name: string; Start: System.DateTime; OwnTime: int<ms>; WaitTime: int<ms>}
        with static member Empty = {Name = ""; Start = new System.DateTime(1900,1,1); OwnTime = 0<ms>; WaitTime = 0<ms>}

    type Dependency = 
        | File of Artifact * Timestamp // regular file (such as source code file), triggers when file date/time is changed
        | ArtifactDep of Target // other target (triggers when target is rebuilt)
        | EnvVar of string * string option // environment variable
        | Var of string * string option // any other data such as compiler version (not used yet)
        | AlwaysRerun // trigger always
        | GetFiles of Fileset * Filelist // depends on set of files. Triggers when resulting filelist is changed
    
    type BuildResult = 
        { Result : Target
          Built : Timestamp
          Depends : Dependency list
          Steps : StepInfo list }

    /// Defines common exception type
    exception XakeException of string

[<AutoOpen>]
module internal ArtifactUtil =    
    /// <summary>
    /// Creates a new artifact
    /// </summary>
    let internal newArtifact name = Artifact name

    // TODO move Artifact stuff out of here

    /// Gets the artifact file name
    let getFullname = function
        | FileTarget file -> file.FullName
        | PhonyAction name -> name

    // Gets the short artifact name
    let getShortname = function
        | FileTarget file -> file.Name
        | PhonyAction name -> name

    /// <summary>
    /// Gets true if artifact exists.
    /// </summary>
    let exists = function
        | FileTarget file -> file.Exists
        | PhonyAction _ ->    false // TODO this is suspicious

    /// <summary>
    /// Gets artifact name
    /// </summary>
    /// <param name="getFullName"></param>
    let getFullName = 
        function 
        | FileTarget f -> f.FullName
        | PhonyAction a -> a
