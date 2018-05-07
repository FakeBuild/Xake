#r "paket: nuget Xake ~> 1.0 prerelease //"
open Xake
open Xake.Tasks.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}