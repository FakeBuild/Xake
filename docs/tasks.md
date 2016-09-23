## Xake Tasks

// TBD

### Common tasks

#### Xake.SystemTasks
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

### File tasks

These tasks allows to perform various file operations. Using these tasks ensures the dependencies are properly resolved are recorded.

* `cp <srcfile> <dest-file-name>` - copies the file
* `rm <mask list>` - removes the files by mask

### Dotnet tasks

Set of tasks to build .NET applications.

* `Csc` - compiles C# files
* `MsBuild` - builds the project or solution using msbuild or xbuild
* `ResGen` - compiles resource file[s]