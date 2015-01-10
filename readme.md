Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target
  * build tool identifies dependencies and build your targets

See [documentation](Overview.md) for more details.

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

See [documentation](Overview.md) for more details.

## References
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
  * [Shake functions reference](http://hackage.haskell.org/package/shake-0.11.4/docs/Development-Shake.html)
