namespace Xake

[<AutoOpen>]
module internal ArtifactUtil =

    /// <summary>
    /// Creates a new artifact
    /// </summary>
    let newArtifact name = Artifact name

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