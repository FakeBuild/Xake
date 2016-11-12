# Xake Tasks

// TBD

## Common tasks

### Xake.SystemTasks
Module shell provides actions related to command shell.
Usage:
```fsharp
open Xake.SystemTasks
let! errorlevel = system (fun o -> o) "ls" ["-lr"]
```

There two predefined function for passing task settings:
```fsharp
open Xake.SystemTasks
let! errorlevel = system (useClr >> checkErrorLevel) "ls" ["-lr"]
```
The first sets `UseClr` which instructs system command to run `mono <cmd>`. The second one instructs **system** to fail when command returned non-zero errorlevel.

> Notice there's another `system` action in Xake.CommonTasks. It lacks the first parameter (for settings) and is marked as obsolete.

## File tasks

These tasks allows to perform various file operations. Using these tasks ensures the dependencies are properly resolved are recorded.

* `cp <srcfile> <dest-file-name>` - copies the file
* `rm <mask list>` - removes the files by mask

### Dotnet tasks

Set of tasks to build .NET applications.

* `Csc` - compiles C# files
* `Fsc` - F# compiler
* `MsBuild` - builds the project or solution using msbuild or xbuild
* `ResGen` - compiles resource file[s]

### NETFX, NETFX-TARGET variables
Xake allows using both Mono and .NET frameworks explicitly by defining `NETFX` variable.
Default behavior is to use the framework the script is running under. E.g. if running under Mono `fsharpi` recent mono toolset will be used.

List of valid targets:

    | "net-20" | "net-2.0" | "2.0"
    | "net-30" | "net-3.0" | "3.0"
    | "net-35" | "net-3.5" | "3.5"
    | "net-40c"| "net-4.0c" | "4.0-client"
    | "net-40" | "net-4.0" | "4.0"| "4.0-full"
    | "net-45" | "net-4.5" | "4.5"| "4.5-full"

    | "mono-20" | "mono-2.0" | "2.0"
    | "mono-35" | "mono-3.5" | "3.5"
    | "mono-40" | "mono-4.0" | "4.0"
    | "mono-45" | "mono-4.5" | "4.5"

Use "2.0".."4.5" targets for mutiplatform environments (will target mono-XXX being run under mono framework).

The following script compiles application using 4.5 framework (mono or .net depending on running environment).

```fsharp
    // xake build file
    #r @"packages/Xake/tools/Xake.Core.dll"
    open Xake

    do xake {ExecOptions.Default with } {
      var "NETFX" "4.5"
      rule ("main" ==> ["hw.exe"])

      rule("hw.exe" *> fun exe -> action {
        do! Csc {
          CscSettings with
            Out = exe
            Src = !! "a.cs"
          }
        })
    }
```

`NETFX-TARGET` variable allow to specify target framework in the similar way, i.e. for all `csc` and `fsc` tasks.

### F# compiler task
Fsc task compiles fsharp project.

```fsharp
do! Fsc {
    FscSettings with
        Out = file
        Src = sources
        Ref = !! "bin/FSharp.Core.dll" + "bin/nunit.framework.dll" + "bin/Xake.Core.dll"
        RefGlobal = ["mscorlib.dll"; "System.dll"; "System.Core.dll"]
        Define = ["TRACE"]
        CommandArgs = ["--optimize+"; "--warn:3"; "--warnaserror:76"; "--utf8output"]
}
```

Fsc uses most recent (from known) compiler version and allows to specify particular version.
 
 * global var `FSCVER` defines version for all fsc tasks.
 * `FscVersion` field in compiler settings. Settings has higher priority.
