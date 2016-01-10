## TODOs and ideas

  * akka.net build scripts
  * idea: xake script as a task. Override/inherit variables. How to change variable on the fly is the original question. (we have got it out of the box, need more info)
  * detect changes in build script (internal changes), e.g. new target added that was not in .xake database
  * dependencies tracking mode: automatically rebuild when dependency is changed, execute triggers allowing to start/stop the processes which lock/hold artifacts
  * in-memory artifact (string or stream). Say in Gulp file is processed in-memory
  * can the rules be abstract over artifacts

### Refactorings
  * Artifact -> FileName of string, relative path

### Rejected

  * accept filemasks in 'need' parameters *-- the use case is very unclear*

## Thoughts
 * idea: rule settings
  * "clean" {FailOnError = true} \*\*> file a -> action {}
  * "clean" \!\*> file a -> action {}
  * "clean" \*\*> file a -> action ({FailOnError = true}) {}
 * folder as a target:
  * want ["Viewer", "Designer"]
  * rule "Viewer" -> fun folder -> action {need [folder <\\> "bin" <\\> folder <.> "exe"]...}
 * extract part of the rule name:
   * example: `"bin/(type:*)/App.exe" *> fun exe -> ...`
   * provides easy access to parts of the path w/o regexps. In this sample 'type' could be extracted as `let! type = getPart "type"`
   * simple match (one part is one variable) vs full-blown (`"bin/(type:*)--(arch:*)--benchmark.exe" *>...` allows to extract part from name)

## Done
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
