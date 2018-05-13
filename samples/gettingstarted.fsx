#r "paket: nuget Xake ~> 1.1 prerelease
           nuget Xake.Dotnet ~> 1.1 prerelease //"
open Xake
open Xake.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}