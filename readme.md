Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target and which targets it depends on
  * build tool identifies dependencies and build your targets

See [documentation](docs/overview.md) for more details.

[![Build Status](https://travis-ci.org/OlegZee/Xake.svg?branch=master)](https://travis-ci.org/OlegZee/Xake)

## The script

The simple script looks like:

```fsharp
#r @"Xake.dll"

open Xake

do xakeScript {
  rules [
    "main" ==> ["helloworld.exe"]

    "*.exe" ..> recipe {
        let! exe = getTargetFullName()
        do! csc {src !!(exe -. "cs")}
    }
  ]
}
```

This script compiles helloworld assembly from helloworld.cs file.

## Getting started

Make sure dotnet SDK 2.0+ is installed.

1. Clone the project:

    ```
    git clone http://github.com/olegzee/xake
    ```
1. Run the "Hello world" build sctipt:

    ```
    cd samples
    dotnet restore dotnet-fake.csproj
    dotnet fake run build.fsx
    ```

## Build the project

Once you cloned the repository you are ready to compile and test the binaries:

```
dotnet restore build.proj
dotnet fake run build.fsx -- build test
```

... or use `build.cmd` (`build.sh`) in the root folder

## Getting started for Mono on Linux/OSX

Make sure mono with F# is installed and root certificates are imported:

```
sudo apt-get install mono-complete
sudo mozroots --import --sync
```

TBD

## Documentation

See [documentation](docs/overview.md) for more details.

## References

* [implementation notes](docs/implnotes.md)
* [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)

## Mono on OSX troubleshooting

Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)

