#r "paket:
    nuget Xake ~> 1.0 prerelease //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#endif

// This a sample Xake script to show off some features.
//
// USAGE:
// * `fake run` or
// * `dotnet restore && dotnet fake run`
// 
// Running particular target:
// * `dotnet fake run build.fsx -- clean`

open Xake
open Xake.Tasks
open Xake.Tasks.Dotnet

do xakeScript {

    consolelog Verbosity.Diag
    want ["main"]   // this is redundant as "main" is default target

    rules [
        "main" <== ["tracetest"; "temp/a.exe"]

        // .NET build rules
        // build .net executable using full .net framework (or mono under unix)

        "clean" => rm {file "temp/a*"}

        "temp/a.exe" ..> csc {src !!"temp/a.cs"}

        "temp/a.cs" ..> writeText
            """
            class Program
            {
            	public static void Main()
            	{
            		System.Console.WriteLine("Hello world!");
            	}
            }
            """

        // `trace` function demo
        // note: output verbosity is set to Diag to display all messages

        "tracetest" => recipe {
            do! trace Message "=============== Sample output follows this line\n\n"

            for loglevel in [Level.Command; Level.Message; Level.Error; Level.Warning; Level.Debug; Level.Info; Level.Verbose] do
                do! trace loglevel "Sample text"
            do! trace Message "\n\n\tend of Sample output follows this line"
        }
    ]
}