module ``Various tests for explicit start``

open System.IO
open NUnit.Framework

open Xake

[<Explicit>]
[<TestCase("mono-35")>]
[<TestCase("mono-40")>]
[<TestCase("mono")>]
[<TestCase("net-35")>]
[<TestCase("net-40")>]
let ``various frameworks``(fwk:string) =

    let p =
      xake
        {ExecOptions.Default with
            Vars = ["NETFX", fwk]
            FileLog = fwk + ".log"
            FileLogLevel = Verbosity.Diag}
        {
          rules [
            "main" => action {
                do! (csc {
                    //targetfwk fwk
                    out (File ("hw" + fwk + ".exe"))
                    src (!! "a.cs")
                    grefs ["mscorlib.dll"; "System.dll"; "System.Core.dll"]
                  })
                }
            "a.cs" *> fun src -> action {
                do File.WriteAllText(src.FullName, """
                class Program
                    {
	                    public static void Main()
	                    {
		                    System.Console.WriteLine("Hello world!");
	                    }
                    }
                """)
                }
            ]
        }

    ()
