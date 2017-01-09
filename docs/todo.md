## TODOs and ideas

  * change the first page to a tutorial with script and usage examples
  * <== for running tasks one by one. Current one runs in parallel only.
  * rules should accept #seq not just the list
  * switch development to mono under windows
  * idea: xake script as a task. Override/inherit variables. How to change variable on the fly is the original question. (we have got it out of the box, need more info)
  * accept filemasks in 'need' parameters (WHY I added it here?, the use case is very unclear)
  * detect changes in build script (internal changes), e.g. new target added that was not in .xake database
  * dependencies tracking mode: automatically rebuild when dependency is changed, execute triggers allowing to start/stop the processes which lock/hold artifacts
  * in-memory artifact (string or stream). Say in Gulp file is processed in-memory
  * can the rules be abstract over artifacts

### Tasks

  * complete copyFiles method

### Refactorings
  * Artifact -> FileName of string, relative path, functions but not methods

## Thoughts
 * idea: rule settings
  * `"clean" {FailOnError = true} \*\*> file a -> action {}`
  * `"clean" \!\*> file a -> action {}`
  * `"clean" \*\*> file a -> action ({FailOnError = true}) {}`
 * folder as a target:
  * `want ["Viewer", "Designer"]`
  * `rule "Viewer" -> fun folder -> action {need [folder <\\> "bin" <\\> folder <.> "exe"]...}`
 * Filelist is not handy as it requires to cast all the time
 * FileInfo is not good for the same reason: poorly composable and does not cover Directory well
 * wildcards phony actions

## Done (top is recent)

 * allow to specify F# compiler version
 * overriding .xake database file name by options.DbFileName which defines relative db file name
 * redirect compiler output to [Info] category, parse output and log warnings and errors respectively
 * changed Artifact type to a File.T
 * files case sensitivity is now platform dependent
 * match groups in rule masks
 * ls returns directory list in case it ends with "/" ("\")
 * MSBuild task
 * performance of rules lookup (takes 2s now)
 * FSC task (f# compiler), self bootstrap
 * command line: pass options, specify sequential/parallel targets
 * progress indicator API (not documented and is not pluggable yet), Windows progress bar indicator
 * let "main" be default rule so that I can skip 'want ["blablabla"]' in most scripts
 * specify target framework for 4.0+ compiler
 * MONO support
   * explicitly target mono
   * configure mono from registry
   * probing paths for tools
 * MSBUILD task
 * CSC resources
 * dependency rule: custom rule (Var)
 * alwaysRerun, +rule with no deps
 * имя файла-результата как аргумент (для оператора **>)
 * задача system
 * правило для группы файлов ("\*.rc" \*\*> fun f -> ...)
 * файл (filepath) с разными операциями
 * списки (fileset)
 * матчинг имен артефактов/файлов + каталоги
 * parameterized filesets (оператор для условной конкатенации списков ["a.cs"] &? debug ["debug.cs"])
 * два вида fileset - правила и вычисленный список
 * CPU affinity as a script option
 * complete abstraction over artifact (do not use fileinfo, resolve files when started using project dir)
 * xake exec parameters (number of threads, log file, verbosity)
 * параметризация сценария, сценарий как объект (main + rules)
 * диагностика и лог (детально в файл, кратко на экран)
 * exception handling and reporting
 * clean (phony actions)
 * do! alwaysRerun() to build target regardless dependencies are untouched
 * incremental builds
   * files
   * database
   * dependency rule: environment variable
   * dependency rule: fileset change
 * условное правило (*?> функция вместо маски)
