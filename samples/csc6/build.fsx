#r @"../../bin/Xake.Core.dll"

open Xake

do xake ExecOptions.Default {
  
  var "NETFX-TARGET" "2.0"
  filelog "build.log" Logging.Diag

  rules [
    "main" ==> ["hello.exe"]
    "hello.exe" *> fun file -> csc {
      cscpath @"packages\Microsoft.Net.Compilers\tools\csc.exe"
      out file
      src !! "hello_cs6.cs"
    }
  ]
}
