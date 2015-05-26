## TODOs

  * xakeArgs: support for sequential running of targets, properties
  * __MONO__ support: pass command line switches
  * resources (CPU, disks etx) for improved load balancing
  * multiple outputs for one rule
  * ~~chocolatey~~ __paket__ support
  * __nuget__ support: retrieve binaries, update packages
  * ...
  * dependency rule: compiler version

## Ideas
  * idea: xake script as a task. Override/inherit variables. How to change variable on the fly is the original question.
  * accept filemasks in 'need' parameters (WHY I added it here?)

## Improvements
  * detect changes in build script (internal changes), e.g. new target added that was not in .xake database
  * performance of rules lookup (takes 2s now)

### Refactorings
  * Artifact -> FileName of string, relative path


## Tasks TODO
  * command-line tool
  * MSBuild task

## In progress

## Done
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

## Thoughts
 * idea: rule settings
  * "clean" {FailOnError = true} \*\*> file a -> action {}
  * "clean" \!\*> file a -> action {}
  * "clean" \*\*> file a -> action ({FailOnError = true}) {}
 * tracing mode: actions are not performed, only need is processed so that we get a dependency graph
 * folder as a target:
  * want ["Viewer", "Designer"]
  * rule "Viewer" -> fun folder -> action {need [folder <\\> "bin" <\\> folder <.> "exe"]...}
