#r "nuget: Xake, 1.1.4.427-beta"
#r "nuget: Xake.Dotnet, 1.1.4.7-beta"

open Xake
open Xake.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}