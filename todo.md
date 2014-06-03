﻿## TODO
  * rules versioning
  * ресурсы (CPU и пр.) для управления очередностью
  * multiple outputs
  * accept filemasks in 'need' parameters (WHY?)
  * ...
  * dependency rule: compiler version
  * dependency rule: custom rule (Var)
  * performance of rules lookup (takes 2s now)

### Refactorings
    * Artifact -> FileName of string, relative path


## Tasks TODO
  * command-line tool
  * CSC resources
  * MSBuild task


## In progress

## Done
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
   * database
   * files
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