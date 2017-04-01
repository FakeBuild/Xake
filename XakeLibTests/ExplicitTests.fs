module ``Various tests for explicit start``

open System.IO
open NUnit.Framework

open Xake
open Xake.Tasks.File
open Xake.Tasks.Dotnet

[<Explicit>]
[<TestCase("mono-35")>]
[<TestCase("mono-40")>]
[<TestCase("mono")>]
[<TestCase("net-35")>]
[<TestCase("net-40")>]
let ``various frameworks``(fwk:string) =

    let p =
      xakeScript {
          var "NETFX" fwk
          filelog (fwk + ".log") Verbosity.Diag

          rules [
            "main" => csc {
                //targetfwk fwk
                out (File.make ("hw" + fwk + ".exe"))
                src (!! "a.cs")
                grefs ["mscorlib.dll"; "System.dll"; "System.Core.dll"]
            }
            "a.cs" ..> writeText """
                class Program
                {
                  public static void Main()
                  {
                    System.Console.WriteLine("Hello world!");
                  }
                }"""
            ]
        }

    ()
