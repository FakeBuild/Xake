Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target and which targets it depends on
  * build tool identifies dependencies and build your targets

See [documentation](docs/overview.md) for more details.

[![Build Status](https://travis-ci.org/OlegZee/Xake.svg?branch=master)](https://travis-ci.org/OlegZee/Xake)

## The script

The simple script might look like:
```fsharp
#r @"Xake.Core.dll"

open Xake

do xake ExecOptions.Default {

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

## Getting started (Mono on Linux/OSX)

Make sure mono with F# is installed and root certificates are imported:
```
sudo apt-get install mono-complete
sudo mozroots --import --sync
```

Follow the steps to compile binaries and get familiar with scripts:

1. Clone the project:

    `git clone http://github.com/olegzee/xake`
1. Build the xake.core dll (to xake/bin folder):
    ```
    cd xake
    fsharpi build.fsx -- get-deps build
    ```
1. Now compile the C# "Hello world" application:
    ```
    cd samples/csc
    fsharpi helloworld.fsx
    ```

## Getting started (Windows)
The build steps for **Windows** are similar to Mono's with a couple differences:

  * open "Developer Command Prompt for VS..." console (so that path points to .NET tool directory)
  * change slashes to a backslashes
  * use `fsi` instead of `fsharpi` to run f# scripts

## Documentation

See [documentation](docs/overview.md) for more details.

## References

  * [implementation notes](docs/implnotes.md)
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)

## Mono on OSX troubleshooting
Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)
