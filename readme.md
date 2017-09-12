Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target and which targets it depends on
  * build tool identifies dependencies and build your targets

See [documentation](https://github.com/OlegZee/Xake/wiki/introduction) for more details.

[![Build Status](https://travis-ci.org/OlegZee/Xake.svg?branch=master)](https://travis-ci.org/OlegZee/Xake)

## The script

The simple script looks like:
```fsharp
#r @"Xake.dll"

open Xake

do xakeScript {
  rules [
    "main" ==> ["helloworld.exe"; "hello.dll"]

    "hello.dll" ..> csc {src (fileset {includes "hello.cs"})}

    "*.exe" ..> recipe {
        let! exe = getTargetFullName()
        do! csc {src !!(exe -. "cs")}
    }
  ]    
}
```

This script compiles helloworld assembly from helloworld.cs file.

## References

  * [wiki documentation](https://github.com/OlegZee/Xake/wiki/introduction) for more details
  * [Old documentation](docs/overview.md) for some details.
  * [implementation notes](docs/implnotes.md)
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
