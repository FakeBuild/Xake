Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

Unlike NAnt, Fake and similar tools with imperative script style, Xake is declarative:

  * you define targets (either files or actions)
  * you describe rules on how to make particular target
  * build tool identifies dependencies and build your targets

## References
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
  * [Shake functions reference](http://hackage.haskell.org/package/shake-0.11.4/docs/Development-Shake.html)

## Mono on OSX troubleshooting
Xake requires 'pkg-config' to locate mono runtime. Pkg-config utility is deployed with mono, but it's not included in
$PATH. The options available are described on [monobjc mailing list](http://www.mail-archive.com/users@lists.monobjc.net/msg00235.html)