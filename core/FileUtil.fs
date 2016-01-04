namespace Xake

[<AutoOpen>]
module internal FileUtil = 
    /// <summary>
    /// Creates a new artifact
    /// </summary>
    let newArtifact name = File name
    
    // TODO move Artifact stuff out of here
    
    /// <summary>
    /// Gets the artifact file name.
    /// </summary>
    let getFullname = 
        function 
        | FileTarget file -> file.FullName
        | PhonyAction name -> name
    
    /// <summary>
    /// Gets the short artifact name.
    /// </summary>
    let getShortname = 
        function 
        | FileTarget file -> file.Name
        | PhonyAction name -> name
    
    /// <summary>
    /// Gets artifact name.
    /// </summary>
    /// <param name="getFullName"></param>
    let getFullName = 
        function 
        | FileTarget f -> f.FullName
        | PhonyAction a -> a
