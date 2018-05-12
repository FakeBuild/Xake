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

        // define a "phony rule", which has goal to produce a file
        "clean" => rm {file "temp/a*"}

        // rule to build an a.exe executable by using c# compiler
        // notice there's no "out" parameter: csc recipe will use the target file as an output
        "temp/a.exe" ..> csc {src (!!"temp/a.cs" + "temp/AssemblyInfo.cs")}

        // the rule above demands a.cs source file, this rule creates the source file
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

        // this rule gets the version from VERSION script variable and generates
        // define the variable by running `dotnet fake run features.fsx -- -d VERSION:2.1.1`
        "temp/AssemblyInfo.cs" ..> recipe {
            let! envVersion = getVar("VERSION")
            let version = envVersion |> Option.defaultValue "1.0.0"
            do! writeText <| sprintf "[assembly: System.Reflection.AssemblyVersion(\"%s\")]" version
        }

        // `trace` function demo
        // note: output verbosity is set to Diag to display all messages (see the "consolelog" instruction on top of xakeScript body)

        "tracetest" => recipe {
            do! trace Message "=============== Sample output follows this line\n\n"

            for loglevel in [Level.Command; Level.Message; Level.Error; Level.Warning; Level.Debug; Level.Info; Level.Verbose] do
                do! trace loglevel "Sample text"

            do! trace Message "\n\n\tend of Sample output follows this line"
        }
    ]
}