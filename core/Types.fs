namespace Xake

[<AutoOpen>]
module DomainTypes =

  type Artifact = FileArtifact of System.IO.FileInfo 
  // TODO have no idea where to put this type and related methods (see fileset.fs) to
