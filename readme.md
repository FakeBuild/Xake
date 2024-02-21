Xake is a build utility that uses the full power of the F# programming language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

[![Build Status](https://travis-ci.org/xakebuild/Xake.svg?branch=dev)](https://travis-ci.org/xakebuild/Xake)

## Sample script

The simple script looks like:

```fsharp
#r "nuget: Xake, 2.0.0"
#r "nuget: Xake.Dotnet, 1.1.4.7-beta"

open Xake
open Xake.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}
```

This script compiles helloworld assembly from helloworld.cs file.

## Getting started

Make sure dotnet SDK 7.0+ is installed.

1. Clone the project:

    ```
    git clone http://github.com/xakebuild/xake
    ```
1. Run the "Hello world" build sctipt:

    ```
    cd samples
    dotnet fsi gettingstarted.fsx
    ```

## Further reading

* See [the features.fsx](https://github.com/xakebuild/Xake/blob/dev/samples/features.fsx) script for various samples.
* We have the [introduction page](https://github.com/xakebuild/Xake/wiki/introduction) for you to learn more about Xake.
* And there're the [documentation notes](https://github.com/xakebuild/Xake/wiki) for more details.

## Build the project

Once you cloned the repository you are ready to compile and test the binaries:

```
dotnet fsi build.fsx -- -- build test
```

... or use `build.cmd` (`build.sh`) in the root folder

## Getting started for Mono on Linux/OSX

> This is untested and mono nowadays is poorly explored territory for me.

Make sure mono with F# is installed and root certificates are imported:

```
sudo apt-get install mono-complete
sudo mozroots --import --sync
```

TBD

## Documentation

See [documentation](docs/overview.md) for more details.

## References

* [documentation](https://github.com/xakebuild/Xake/wiki) 
* [implementation notes](docs/implnotes.md)
* [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
* [samples repository](https://github.com/xakebuild/Samples)

## Mono on OSX troubleshooting

Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)

