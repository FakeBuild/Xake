# Implementation notes
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
Filesets are used to references both existing files (e.g. project source files) and **targets** which might not exists when fileset is "materialized". In case the file or directory name is treated as a mask, the non-existing file will be omitted from resulting file list and the build will likely fail.
The rule to resolve such inconsistency the rule without mask is added to resulting file list explicitly.

NOTE: it should depend on the context: if fileset defines source files or references "explicit rule" is ok.

## Error handling and diagnostics
Error handling assumes the following system behavior:

  * system provide screen and file log for all targets built and other system actions
  * allows to specify detailization level for screen and file separately
  * any uncaught exception in build rule leads to script failure unless FailOnError property is for particular target is set to False
(consider "deploy" target with a fallback implementation)
  * failures and stack traces are written to log
  * idea: "onfail" target, "onfail" rule setting
  * idea: dump the whole trace to the target

### Ideas in progress
Idea #1: IgnoreErrors function which intercepts any failures silently
```
  phony "main" (action {
    do! trace Message "The exception thrown below will be silently ignored"
    failwith "some error"
    } |> IgnoreErrors)
```

Idea #2 (orthogonal): provide an option for _system function to fail in case non-zero errorcode.
```
do! _system [fail_on_error; shellcmd] "dir"
// where shellcmd and fail_on_error are functions
```

Idea #3 (orthogonal): special directive to fail next command on non-zero result
```
fail_on_errorlevel _system [fail_on_error; shellcmd] "dir"
// where shellcmd and fail_on_error are functions
```


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
Xake allows using both Mono and .NET frameworks explicitly by defining NETFX variable.
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

Tasks:
  * various supported options for csc

## Variables

 * NETFX - framework version to use for compilation, resources. E.g. "2.0", "3.5", "4.0". Default: highest available on the computer


## Command line parameters

Script arguments allow to specify execution options, list of targets, logging options and others from command line.
According to fsi.exe "--" arguments denotes the start of script arguments so in the most common case you will use it as:

  fsi.exe build.fsx -- clean build deploy

where "clean" "build" and "deploy" are target names.

The full list of parameters:

 * -h -- displays help screen
 * -t <task count> -- use <task count> simultaneous processes to execute the build tasks. * Default value is the number of processors
 * -r <root path> -- override the root path. All the targets and filesets are resolved relatively to this path. Default is current directory
 * -ll <log level> -- console log level (Silent | Quiet | Normal | Loud | Chatty | Diag)
 * -fl <file log path> -- specifies the name of the log file
 * -fll <log level> -- specifies the logging level to a log file
 * target1 .. targetN -- define the list of targets. Targets are executed in strict order, the second one starts only after the first one is completed.
 * target1;target2;..targetN -- execute the targets simultaneously
 * -d <name>=<value> -- defines a script variable value
 * -nologo -- remove logo string

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

```
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
  * `Csc` and other tasks has an `Out` parameter which got `File.T` type. This is not going to be user friendly. And I should consider changing it to string. However in most cases this value is passed from action parameters
  so the types should be coherent

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
