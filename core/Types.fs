namespace Xake

[<AutoOpen>]
module DomainTypes =

  type Target = FileTarget of System.IO.FileInfo | PhonyAction of string
  // TODO have no idea where to put this type and related methods (see fileset.fs) to
