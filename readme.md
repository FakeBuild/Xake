Xake is a build utility that uses the full power of the F# programming language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

[![Build Status](https://travis-ci.org/xakebuild/Xake.svg?branch=dev)](https://travis-ci.org/xakebuild/Xake)

## Sample script

The simple script looks like:

```fsharp
#r "paket: nuget Xake ~> 1.1 prerelease //"
open Xake
open Xake.Tasks.Dotnet

do xakeScript {
  rules [
    "main" => need ["helloworld.exe"]
    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}
```

This script compiles helloworld assembly from helloworld.cs file.

To run this script:

1. Clone the project:

    ```
    git clone http://github.com/xakebuild/xake
    ```
1. Run the "Hello world" build sctipt:

    ```
    cd samples
    dotnet restore dotnet-fake.csproj
    dotnet fake run gettingstarted.fsx
    ```

## Further reading

* See [the features.fsx](https://github.com/xakebuild/Xake/blob/dev/samples/features.fsx) script for various samples.
* We have the [introduction page](https://github.com/xakebuild/Xake/wiki/introduction) for you to learn more about Xake.
* And there're the [documentation notes](https://github.com/xakebuild/Xake/wiki) for more details.

## Build the project

Once you cloned the repository you are ready to compile and test the binaries:

```
dotnet restore build.proj
dotnet fake run build.fsx -- build test
```

... or use `build.cmd` (`build.sh`) in the root folder

## References

* [implementation notes](docs/implnotes.md)
* [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
* [samples repository](https://github.com/xakebuild/Samples)

## Mono on OSX troubleshooting

Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)

