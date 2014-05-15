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
When the target is present and it's build time is greater than any of the files it depends on (defined using **need** function) the rule is not executed.
Initially the file-system time will be used, and we'll reconsider shake's approach to store last build time in database.

The implementation idea is to modify *action* computation to skip any steps after **need** if that one returned "No modification" status. However there're might be multiple **need** steps so we cannot skip just any action, and, at least, we should check all **need**s.

The plan is as follows:

 * In case the "incremental build" option is not set **xake** behaves as usual, no changes
 * introduce IncrementalBuildStatus field in action context
 * if **need** returned "No modification" the status is changed to "Skip"
 * is "Skip" is set, any actions are skipped, except...
 * The **need** custom operation is not skipped and
  * if it returns "no modification" the computation continues
  * in case of any other status the exception is thrown, and user is suggested to change script to meet incremental build needs

The other "naive" option is to check status in any task/function we implement and just skip execution. This approach does not expose enough "magic".

Challenges:

  * pass artifact name to context/Action<> monad
  * skip execution of action in context enforces it
  
### Other

  * file names are cases sensitive now. In the future it's planned to be system-dependent

