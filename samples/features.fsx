#r "paket:
    nuget Xake ~> 1.1 prerelease
    nuget Xake.Dotnet ~> 1.1 prerelease //"

#if !FAKE
#load ".fake/features.fsx/intellisense.fsx"
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
open Xake.Dotnet

do xakeScript {

    consolelog Verbosity.Diag

    // this instruction defines the default target and could be overriden by command-line parameters
    // this is redundant as "main" is default target
    want ["main"]

    rules [

        // this rule does nothing but demands the other targets
        // the execution of the recipe is suspended until all demanded targets are built.
        // Targets are executed in parallel. Dependencies could be demanded in any part of recipe.
        "main"  => recipe {
            do! need ["tracetest"; "temp/a.exe"]
            }

        // this is shorter way to express the same. See also `<==` and '<<<' operators.
        "main"  => need ["tracetest"; "temp/a.exe"]

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


        // defining the target which produces multiple files
        // recipe will be executed just once, regardless how many times its outcome was requested in other targets
        // notice the `*..>` operator is used
        ["app.exe"; "app.xml"] *..> recipe {

            let! [appfile; xmlfile] = getTargetFiles()

            do! Fsc {
                FscSettings with
                    Src = fileset {
                        basedir "core"
                        includes "Logging.fs"
                        includes "Program.fs"
                    }
                    Out = appfile
                    Ref = !! "bin/FSharp.Core.dll"
                    RefGlobal = ["System.dll"; "System.Core.dll"]
                    CommandArgs = ["--utf8output"; "--doc:" + xmlfile.FullName]
            }
        }

        // using wildcards and named groups when defining target
        // The following rule defines how to build any file which names matches "*/*.cs*" pattern.
        // Round brackets specify the named groups (known from regexps), the values can be accessed via getRuleMatch function
        // e.g. for src/hello.csx the matches will be: dir=src, file=hello, ext=csx

        "(dir:*)/(file:*).(ext:c*)" ..> recipe {

            let! dir = getRuleMatch "dir"
            let! file = getRuleMatch "file"
            let! ext = getRuleMatch "ext"

            // here you place regular build steps

            return ()
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