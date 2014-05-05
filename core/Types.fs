namespace Xake

[<AutoOpen>]
module DomainTypes =

  type Artifact = System.IO.FileInfo
  type Target = FileTarget of Artifact | PhonyAction of string
  // TODO have no idea where to put this type and related methods (see fileset.fs) to
