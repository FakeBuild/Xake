Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

The simple script looks like:
```fsharp
#r @"Xake.Core.dll"

open Xake

do xakeScript {

  rules [
    "main" ==> ["helloworld.exe"]
    "helloworld.exe" ..> csc {src !! "helloworld.cs"}
}
```
This script compiles helloworld assembly from helloworld.cs file.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets, such as "main" (either files or actions)
  * you describe rules on how to make particular target and which targets it depends on
  * build tool identifies dependencies and build your targets

See [wiki documentation](wiki) for more details.

[![Build Status](https://travis-ci.org/OlegZee/Xake.svg?branch=master)](https://travis-ci.org/OlegZee/Xake)

## References

  * [documentation wiki](wiki)
  * [implementation notes](docs/implnotes.md)
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
