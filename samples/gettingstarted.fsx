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