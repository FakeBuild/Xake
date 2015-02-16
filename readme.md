Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target
  * build tool identifies dependencies and build your targets

See [documentation](docs/overview.md) for more details.

[![Build Status](https://travis-ci.org/OlegZee/Xake.svg?branch=master)](https://travis-ci.org/OlegZee/Xake)

## The script

The simple script might look like:
```fsharp
#r @"Xake.Core.dll"

open Xake

do xake XakeOptions {

  rule ("main" ==> ["helloworld.exe"])

  rule("*.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! (exe.Name -. "cs")
      }
    })

}
```

This script compiles helloworld assembly from helloworld.cs file.

## Getting started (Mono and Microsoft's .NET framework)

Clone the project:

    git clone http://githib.com/olegzee/xake

Build the xake.core dll:

    cd xake
    xbuild xake.sln

Run the "Hello world" sample:

    fsharpi samples/build.fsx

Now compile the C# "Hello world" application:

    cd samples/csc
    fsharpi helloworld.fsx

> The build steps for Microsoft's .NET are pretty much the same except:

>  * open Developer Tools console (so that path points to .NET tool directory)
>  * use msbuild instead of xbuild
>  * change slashes to a backslashes
>  * use fsi instead of fsharpi to fun f# scripts

See [documentation](docs/overview.md) and [implementation notes](docs/implnotes.md) for more details.

## References
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
  * [Shake functions reference](http://hackage.haskell.org/package/shake-0.11.4/docs/Development-Shake.html)

## Mono on OSX troubleshooting
Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)
