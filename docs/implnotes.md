﻿# Implementation notes

## Fileset

Implementation is very similar to NAnt's one, it does support:

* disk reference
* parent dir reference
* directories and directory masks
* recurse mask (e.f. "source/**/*.c"), supports
* file name and mask
* both "/" and "\" as a path separator

Fileset has two major subtypes - either **set of rules** or a **file list**, also called "materialized" fileset.

### Non-existing files

Filesets are used to reference both existing files (e.g. project source files) and **targets** which might not exist when fileset is "materialized". In case the file or directory name is treated as a mask, the non-existing file will be omitted from resulting file list and the build will likely fail.
The rule to resolve such inconsistency the rule (part of pattern) without mask is added to resulting file list explicitly.

NOTE: it should depend on the context: if fileset defines source files or references "explicit rule" is ok.

## Error handling and diagnostics

Error handling assumes the following system behavior:

* system provide screen and file log for all targets built and other system actions
* allows to specify detail level for screen and file separately
* any uncaught exception in build rule leads to script failure unless FailOnError property is for particular target is set to False (consider "deploy" target with a fallback implementation)
* failures and stack traces are written to log
* idea: "onfail" target, "onfail" rule setting
* idea: dump the whole trace to the target
* setting error-code for the fsi sessions

### Implemented ideas

#### try/with/finally exception handling

`recipe` computation expression supports try/with and try/finally blocks.

```fsharp
recipe {
  do! log "before try"

  try
    try
        do! log "Body executed"
        failwith "Ouch"
    with e ->
        do! log "Exception: %s" e.Message
  finally
    printfn "Error!"
}
```

> actions (with do! notation) are allowed in `with` block but aren't in `finally` block. This is limitation of F#'s computation expressions.

#### WhenError function

Intercepts errors (exceptions) and allows to define a custom handler.

```fsharp
  phony "main" (action {
    do! trace Message "The exception thrown below will be silently ignored"
    failwith "some error"
    } |> WhenError ignore)
```

#### FailWhen

Raises the exception if action's result meet specified condition.
E.g. the following code raises error in case errorlevel (result of shell command execution) gets non-zero value.

```fsharp
do! _system [shellcmd] "dir" |> FailWhen ((<>) 0) "Failed to list files in folder"
// or just
do! _system [shellcmd] "dir" |> CheckErrorLevel
```

### Other ideas

// or even that:

```fsharp
_system [fail_on_error; shellcmd] "dir"
// where shellcmd and fail_on_error are functions
```

Idea #3 (orthogonal): provide an option for _system function to fail in case non-zero errorcode.

```fsharp
do! _system [fail_on_error; shellcmd; startin "./bin"] "dir"
// where shellcmd and fail_on_error are functions
```

### Ideas

Implemented IgnoreErrors.

* ExecContext option to ignore all errors
* fail on system with non zero exit code
* fail always try/catch  

## Incremental build

Xake attempts to reduce build time by analyzing results of last build. Build rule is executed if any of these conditions are met:

* any of dependency source files are changed
* dependency artifact was rebuilt
* there no dependencies at all (e.g. "clean" task), otherwise with incremental builds it will never rebuild
* action is marked with alwaysRerun
* environment variable or script variable the script or any task requests is changed

### Implementation: Analyze last run

This option is found in "shake" project. It stores all targets and their dependencies in database. Whenever the target is requested it checks
whether it needs to be built by analyzing whether any dependencies are changed.
The benefits of this approach in comparison to option 1 are:

* easier to implement due to more straightforward logic
* allows to analyze the whole dependencies graph before running any target
* ... and estimate the build time
* does not require changes in tasks implementation ("whenNeeded")

The difference is that the decision is made early, before executing target while option 1 does execute the code and makes a decision upon request.

### Random thoughts

GetFiles will be new monadic function. Record the call in dependencies as GetFileList(), both rules and results. Track only different results.
Need is traced as before i.e. for every "need" we record the exec time and target name.

## .NET Runtime
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

Use "2.0".."4.5" targets for multi-platform environments (will target mono-XXX being run under mono framework).

Tasks:* various supported options for csc

## Variables

 * NETFX - framework version to use for compilation, resources. E.g. "2.0", "3.5", "4.0". Default: highest available on the computer. 


### Do not allow to override options

Command line arguments override the script options (XakeOptions type) unless you define options.IgnoreCommandLine = true.

## Propose: named match groups in file or directory masks

Allows to extract substring from a file or directory masks. Handy for defining
"parameterized" rules. According to product idea any artifact is unique and has its
own file so any parameterized rule is resolved to a file.

E.g. `"bin\(type:*)\Application.exe"` defines a mask with a named part referencing directory.
The call to `match mask "bin\Debug\Application.exe"` will result in `MatchResult.Ok ["type", "Debug"]`.

Named groups Mask is defined either for DirectoryMask of FileMask.
Nested groups are ok too, e.g. `"(filename:(arch:*)-(platform:*)-lld).(ext:*)"` matches the file
`x86-win-lld.app` and returns map {"filename", "x86-win-lld"; "arch", "x86"; "platform", "win"; "ext", "app"}

```fsharp
var mask = "(arch:*)-(platform:*)-autoexec.(ext:*)";
var mask2 = "(filename:(arch:*)-(platform:*)-lld).(ext:*)";

var mm = Regex.Match ("x86-win-autoexec.bat", @"(?<filename>(?<arch>.*)-(?<platform>.*)-autoexec)\.(?<ext>.*)");
mm.Groups["arch"].Dump();
mm.Groups["platform"].Dump();
mm.Groups["ext"].Dump();
mm.Groups["filename"].Dump();
```

## Other

* file names are cases sensitive now. In the future it's planned to be system-dependent
* external libraries has to be copied to target folder. How to accomplish?
  * csc task option "copy deps to output folder"
  * manually copy (need tracking which are really needed)
  * explicit external map of deps: use both

## File/Target and other types

Made File a module with T type which details are hidden. API is exposed as functions within a File module and
also some widely used properties are available as File.T members.

The reason for 'Name' property is a user-friendly output and I'm going to change it to a relative names.
Expected the following issues:

* File functions operate on File.T type which is not usable in user scripts
> Resolution: script will not use this type, instead we will expose FileTasks and tell to use System.IO.File
* `Csc` and other tasks has an `Out` parameter which got `File.T` type. This is not going to be user friendly. And I should consider changing it to string. However in most cases this value is passed from action parameters so the types should be coherent

The motivations are:

* to be more statically typed internally. This is the reason for not using strings.
* FileInfo is a poorly collected garbage. I'd use both internally and externally more accurate abstraction
* FileInfo is unlikely Unix-friendly and allows comparison and such things
* provide more nice operators for end-user, let combine the paths, change extensions and so on
* more coupled integration with Path type (WHAT?)
* attempt to make abstract `Artifact` entity which would allow to define not only files but say in-memory data streams or byte arrays. In such terms phony actions could be regular rules producing no file.

The decision points:

* use File everywhere
* Expose the type but primarily for internal use
* reconsider out parameter (change to string) - check the pros and cons

### Build notes

Release new version by tagging the commit:

    git tag v0.3.6
    git push --tags

#### Running particular test from command line

    fsi build.fsx -- test -d "WHERE=test==\"SystemTasksTests.shell\""
