<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
**Table of Contents**  *generated with [DocToc](https://github.com/thlorenz/doctoc)*

  - [The first script](#the-first-script)
    - [Bootstrapping Xake.Core](#bootstrapping-xakecore)
  - [So what?](#so-what)
    - [Dependencies tracking](#dependencies-tracking)
    - [Running multiple rules in parallel](#running-multiple-rules-in-parallel)
- [Build script elements](#build-script-elements)
  - [Script header](#script-header)
  - ["Main" function](#main-function)
    - [rule](#rule)
    - [file patterns, named groups](#file-patterns-named-groups)
    - [phony](#phony)
    - [rules](#rules)
    - [want](#want)
    - [wantOverride](#wantoverride)
  - [action computation](#action-computation)
    - [Tasks, `do!` notation](#tasks-do-notation)
    - [Exception handling](#exception-handling)
    - [need](#need)
    - [Filesets](#filesets)
    - [Other functions](#other-functions)
    - [Script variables](#script-variables)
    - [Env module](#env-module)
    - [Tasks](#tasks)
      - [File tasks](#file-tasks)
      - [Dotnet tasks](#dotnet-tasks)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

﻿Xake script is just an F# script with some flavors.

## The first script

The most simple, but structured script looks as follows:

```fsharp
#r @".tools/Xake.Core.dll"       // (1)

open Xake                           // (2)

do xake ExecOptions.Default {               // (3)

  "main" <== ["hw.exe"]             // (4)

  rule("hw.exe" *> fun exe -> action {  // (5)
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a*.cs"
      }
    })
}
```

Here are we doing the following steps:

1. reference f# library containing core testing functionality
1. open Xake namespace, so that we can use some Xake types
1. define a "main" function of a build script
1. specify the default target ("main") requires "hw.exe" target
1. define the rule for "hw.exe" target

### Bootstrapping Xake.Core

The steps above assumes you've downloaded xake core assembly to .tools folder.
The next script demonstrates how to create the build script that does not require any installation steps:

```fsharp
// boostrapping xake.core
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let file = System.IO.Path.Combine("packages", "Xake.Core.dll")
if not (System.IO.File.Exists file) then
    printf "downloading xake.core assembly..."; System.IO.Directory.CreateDirectory("packages") |> ignore
    let url = "https://github.com/OlegZee/Xake/releases/download/v0.3.5/Xake.Core.dll"
    use wc = new System.Net.WebClient() in wc.DownloadFile(url, file + "__"); System.IO.File.Move(file + "__", file)
    printfn ""

// xake build file body
#r @"packages/Xake.Core.dll"

open Xake

do xake {ExecOptions.Default with FileLog = "build.log"; Threads = 4 } {

  rule ("main" ==> ["helloworld.exe"])

  rule("*.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! (exe.Name -. "cs")
      }
    })

}
```

## So what?
Pretty much the same result could be obtained in traditional build system without ever mentioning declarative approach. However `xake` will not only create the requested binaries but would also remember the rules it followed and any dependencies it discovered.

### Dependencies tracking
The information recorded during the build allows `xake` to avoid redundant actions during current and subsequent runs.
Particularly in this example it will record:

* "main" depends on "hw.exe"
* "hw.exe" rule requires csc compliler of highest available version
* "hw.exe" depends on `a*.exe` file mask
* `a*.*` was resolved to a single file `a.cs`
* the date/time of the `a.cs` is '2014-12-25 23:57:01'

And during next run it will execute `main` rule only if at least one of following conditions is met:

* there's no hw.exe
* you've installed newer .NET framework or removed the latest one
* file mask `a*.cs` resolves to a different file list
* the date of the `a.cs` was changed

### Running multiple rules in parallel

The other benefit the declarative approach brings is a parallel execution. Whenever `xake` see there's another pending task and free CPU core it executes the task. Maximal number of simultaneously executed tasks is controlled by a `XakeOptions.Threads` parameter is set by default to a number of processors (cores) in your system.

And these both benefits do not require any additional efforts from you if you follow several simple rules.

# Build script elements

You've seen the structure of the script above. Let's reiterate it.

## Script header

You define the *references* to assemblies defining the tasks (if any) and you add the reference to main `xake.core.dll` assembly. You can also define *functions, variables and constants* here.

## "Main" function

In fact this block is just the call to `xake` which is a special kind of computation expression accepting only the elements described below.

### rule
Defines a rule for making file.

Example:

``` fsharp
rule ("out\\Tools.dll" *> fun outname -> action {

    do! Csc {
        CscSettings with
            Out = outname
            Src = !! "Tools/**/*.cs"
            Ref = !! "out\\facade.dll"
    }
})
```


There're several forms of rules including:

* `rule (<file pattern> %> fun out -> <action>)` - rule for single file or group of files matching the specified wildcards pattern. The file and an optional matching groups will be passed to `out` argument of type RuleActionArgs
* `rule (<condition> *?> fun outname -> <action>)` - allows to use function instead of file name or wildcards
* `rule (<name> => <action>)` - creates a phony rule (the rule that does not create a file)
* `rule (<name> <== [targets])` - creates a phony rule which demands specified targets
* `rule (<file pattern> *> fun outname -> <action>)` - the same as `%>` but the file is passed to action. Outdated option.

> Notice: you are not bound to `outname` name above, you could change it to any other name.

> Notice: the whole rule or rule action could be defined outside the `main` block. But you have to register the rule in main block. See the following example:

```fsharp
#r @"../../bin/Xake.Core.dll"

open Xake

let mainRule = "hw.exe" *> fun exe -> action {
    do! Csc {
      CscSettings with
        Out = exe
        Src = !! "a.cs"
      }
    }

do xake {ExecOptions.Default with Threads = 4} {

  phony "build" (action {
      do! need ["hw.exe"]
      })

  rule mainRule
}
```

### file patterns, named groups

`file pattern` allows to define regular Ant-like patterns including name wildcards, recursion wildcard (`**`) and also **named groups**.

E.g. the pattern `"(plat:*)-a.ss"` will match wildcard `"*-a.ss"` pattern and store '\*' part to a 'plat' group.
``` fsharp
do xake {XakeOptions with Targets = ["out/abc.ss"]} {
    rule ("(dir:*)/(file:*).(ext:ss)" %> fun out -> action {
            
        Assert.AreEqual("out", out.group "dir")
        Assert.AreEqual("abc", out.group "file")
        Assert.AreEqual("ss", out.group "ext")
        matchedAny := true
    })
}
```

### phony

The same as `=>` above. Just another alias.

### rules

Allows to specify multiple rules passed in array. Syntactical sugar for reducing number of brackets.

``` fsharp
rules [

  "main"  <== ["build"]
  "build" <== ["out\\tools.dll"; "out\\main.exe"]

  "out\\main.exe" *> fun outname -> action {

    do! Csc {
        CscSettings with Out = outname
            Src = !! "Main/**/*.cs" + "Common/*.cs"
            Ref = !! "out\\tools.dll"
    }
  }

  "out\\tools.dll" *> fun outname -> action {

    do! Csc {
        CscSettings with Out = outname
            Src = !! "Tools/**/*.cs"
            Ref = !! "out\\facade.dll"
    }
  }

  "out\\facade.dll" *> fun outname -> action {

    do! Csc {
        CscSettings with Out = outname
            Src = !! "facade/**/*.cs"
    }
  }
]
```

### want

Defines a default list of targets in case it was not set in script parameters (e.g. XakeOptions.Wants).

### wantOverride

The same as above but overrides the list of targets passed via parameters.

### dryrun

The `dryrun` keyword in the script instructs xake to simulate execution of the script without running the rules itself. Xake tool displays the dependencies list and execution time estimates. Respective command-line option is `--dryrun`.

### filelog

This option allows to specify the name of the log file and the detailization level.

```fsharp
    do xake XakeOptions {
        dryrun
        filelog "errors.log" Verbosity.Chatty
        rules [
   ...
```

## action computation

Action body is computation expression of type *action*. This computation returns *Action* type and is very similar to
*async* computation. You could use both regular code (such as assignments/binding, loops and conditional expressions)
and do! notation within *action* body.

See the functions allowing to access execution context within *action* body.

### Tasks, `do!` notation

`do!` allows executing both async methods and *tasks*. *Task* is a regular F# function that is an *action* so that it returns *Action* type.

Tasks are very simple:
```fsharp
/// Copies the file
let cp (src: string) tgt =
  action {
    do! need [src]
    File.Copy(src, tgt, true)
  }
```

In case the action returns a value you could consume it using let-bang:
```fsharp
  action {
    let! error_code = system "ls" []
    if error_code <> 0 then failwith...
  }
```

If the task (action) returns a value which you do not need use Action.Ignore:
```fsharp
  action {
    do! system "ls" [] |> Action.Ignore
  }
```

### Exception handling

`action` block allows to handle exceptions with idiomatic try/with and try/finally blocks.
```fsharp
    phony "main" (action {
      do! trace Level.Info "before try" // trace is standard action reporting to file/screen log
      try
        try
            do! trace Level.Info "try"
            failwith "Ouch"
        with e ->
            do! trace Level.Error "Error '%s' occured" e.Message
      finally
          printfn "Finally executed"
      printfn "execution continues after try blocks"
    })
```
Notice `trace` function just like any other actions (do! notation) cannot be used in `finally` blocks due to language limitations.

`WhenError` function is another option to handle errors.
```fsharp
action {
    printfn "Some useful job"
    do! action {failwith "err"} |> WhenError (fun _ -> printfn "caught the error")
}
```
or this way
```fsharp
rules [
    "main" => (
        WhenError ignore <| // ignore is a standard f# function accepting one argument
        action {
            printfn "Some useful job"
            failwith "File IO error!"
            printfn "This wont run"
        })
]
```

### need

`need` function is widely used internally and it is a key element for dependency tracking. Calling `need` ensures the requested files are built according to rules.
The action is paused until all dependencies are resolved and built.

> In fact `need` is smart enough and it checks dependencies to determine whether to build file (execute respective rule) or not. In case you need the same file for multiple targets xake will build it only once.
> In case you need the dependency to rebuild every time it's requested you can use `alwaysRerun()` function described below.

In the sample above `cp` function ensures the source file is build before it's copied to target folder.

### Filesets

* `getFiles` - (only allowed inside action) returns list of files specified by a fileset
* `ls` - creates a fileset for specified file mask. In case mask ends with "/" or "\" it returns directory list

File masks follow Ant/Fake rules.

### Other functions

* `trace <level> <format> <args...>`
* `getCtxOptions` - gets action context
* `getVar <varname>` - gets the variable value (and records dependency!)
* `getEnv <varname>` - gets environment variable (and records dependency!)
* `alwaysRerun` - instructs Xake to rebuild the target even if dependencies are not changed

### Script variables

Script variables are not F# variables.

> TBD

### Env module
Provides information about environment.

Methods are:
* `isRunningOnMono` - executing under mono runtime (both in Windows and Unix)
* `isUnix` - is `true` is executing in Unix/OSX/Linux operating system
* `isWindows` - -,,- Windows operating system

```fsharp
open Xake.Env

let! _ = system (if isWindows then "dir" else "ls")
```
### Tasks

#### File tasks

These tasks allows to perform various file operations. Using these tasks ensures the dependencies are properly resolved are recorded.
> TBD

* `copyFile <srcfile> <dest-file-name>` - copies single file (tracks dependency)
* `rm <mask list>` - removes the files by mask

#### Dotnet tasks

Set of tasks to build .NET applications.

* `Csc` - compiles C# files
* `MsBuild` - builds the project or solution using msbuild or xbuild
* `ResGen` - compiles resource file[s]
