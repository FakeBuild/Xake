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

## Incremental build
Xake attempts to reduce build time by analyzing results of last build. The following rules are implemented:

 * if any of dependency source files are changed
 * if dependency artifact was rebuilt
 * if there no dependencies at all (e.g. "clean" task), otherwise with incremental builds it will never rebuild
 * in case action is marked with alwaysRerun

### Option 1. Check the status on the fly
The idea is that we track the "need" status internally and in case any of dependencies is built or refreshed (file date is different) the status is changed to
"Rebuild". There's a function called 'whenNeeded' which executes given action only if status is set.

Rebuild is required when:

 * there's no target
 * dependency is changed (was rebuilt or date is changed)
 * dependencies list is changed

This solution requires the last values of dependencies to be stored and in particular:

 * target build time
 * the list of direct dependencies (there's dependency removed or added)

### Option 2. Analyze last run
This option is found in "shake" project. It stores all targets and their dependencies in database. Whenever the target is requested it checks whether it needs to be built by analyzing whether any dependencies are changed.
The benefits of this approach in comparison to option 1 are:

 * easier to implement due to more straightforward logic
 * allows to analyze the whole dependencies graph before running any target
 * ... and estimate the build time
 * does not require changes in tasks implementation ("whenNeeded")

The difference is that the decision is made early, before executing target while option 1 does execute the code and makes a decision upon request.

### Random notes
When the target is present and it's build time is greater than any of the files it depends on (defined using **need** function) the rule is not executed.
Initially the file-system time will be used, and we'll reconsider shake's approach to store last build time in database.

The implementation idea is to modify *action* computation to skip any steps after **need** if that one returned "No modification" status. However there're might be multiple **need** steps so we cannot skip just any action, and, at least, we should check all **need**s.

The other "naive" option is to check status in any task/function we implement and just skip execution. This approach does not expose enough "magic".

Challenges:

  * pass artifact name to context/Action<> monad
  * skip execution of action in context enforces it
  
### Other

  * file names are cases sensitive now. In the future it's planned to be system-dependent

