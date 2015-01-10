Xake script is just an F# script with some flavors.

## The first script

In order to use xake add the reference to core xake library:

``` fsharp
    #r @"../../bin/Xake.Core.dll"
 ```

The most simple, but structured script looks as follows:

```fsharp
#r @"../../bin/Xake.Core.dll"       // (1)

open Xake                           // (2)

do xake XakeOptions {               // (3)

  want (["build"])                  // (4)

  phony "build" (action {           // (5)
      do! need ["hw.exe"]
      })

  rule("hw.exe" *> fun exe -> action {  // (6)
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
1. open Xake namespace, so that we can use some Zake types 
1. define a "main" function of a build script
1. specify the target
1. define the rule for "build" target
1. define the rule to create "hw.exe" artifact

### So what?
Pretty much the same result could be obtained in traditional build system without ever mentioning declarative approach. However `xake` will not only create the requested binaries but would also remember the rules it followed and any dependencies observed. This information allows `xake` to avoid redundant actions during current and subsequent runs.

Particularly in this example it will record:

* "build" depends on "hw.exe"
* "hw.exe" rule requires csc compliler of highest available version
* "hw.exe" depends on `a*.exe` file mask
* `a*.*` was resolved to a single file `a.cs`
* the date/time of the `a.cs` is '2014-12-25 23:57:01'

And during next run it will execute `build` rule only if at least one of following conditions is met:

* there's no hw.exe
* you've installed newer .NET framework or removed the latest one
* file mask `a*.cs` resolves to a different file list
* the date of the `a.cs` was changed

### Running multiple rules in parallel

The other benefit the declarative approach brings is a parallel execution. Whenever `xake` see there's another pending task and free CPU core it executes the task. Maximal number of simultaneously executed tasks is controlled by a `XakeOptions.Threads` parameter is set by default to a number of processors (cores) in your system.

And these both benefits do not require any additional efforts from you if you follow several simple rules.

## Build script elements

You've seen the structure of the script above. Let's reiterate them.

### Script header

You define the *references* to assemblies defining the tasks (if any) and you add the reference to main `xake.core.dll` assembly. You can also define *functions, variables and constants* here.

### "Main" function

In fact this block is just the call to `xake` is a special kind of so called computation expression which accepts only the elements defined below.

#### rule
Defines a rule for making file.

Example:

``` fsharp
rule "out\\Tools.dll" *> fun outname -> action {

    do! Csc {
        CscSettings with
            Out = outname
            Src = !! "Tools/**/*.cs"
            Ref = !! "out\\facade.dll"
    }
}
```


There're several forms of rules including:

* `rule <file pattern> \*> fun outname -> <action>` - rule for single file or group of files matching the specified wildcards pattern. The actual name (in case of wildcards pattern) will be passed to `outname` parameter
* `rule <condition> \*?> fun outname -> <action>` - allows to use function instead of file name or wildcards
* `rule <name> => <action>` - creates a phony rule (the rule that does not create a file)

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

do xake {XakeOptions with Threads = 4} {

  want (["build"])

  phony "build" (action {
      do! need ["hw.exe"]
      })

  rule mainRule
}
```

#### phony

The same as `=>` above. Just another alias.

#### rules

Allows to specify multiple rules passed in array. Syntactical sugar.

``` fsharp
rules [
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

#### want

Defines a default list of targets in case it was not set in script parameters (e.g. XakeOptions.Wants).

#### wantOverride

The same as above but overrides the list of targets passed via parameters.

### Rule action

> TBD
