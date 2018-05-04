module NUnit.Framework

#if NETCOREAPP2_0

type PlatformAttribute(platforms: string) =
    // inherit System.Attribute()
    inherit NUnit.Framework.IgnoreAttribute("Platform is not supported")
    member val Platforms: string = platforms with get, set

#endif
