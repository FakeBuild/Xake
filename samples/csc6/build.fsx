#r @"../../bin/Xake.Core.dll"

open Xake
open Xake.Tasks.Dotnet

do xake ExecOptions.Default {
  
  var "NETFX-TARGET" "2.0"
  filelog "build.log" Logging.Diag

  rules [
    "main" ==> ["hello.exe"]
    "hello.exe" ..> csc {
      cscpath @"packages\Microsoft.Net.Compilers\tools\csc.exe"
      src !! "hello_cs6.cs"
    }
  ]
}
