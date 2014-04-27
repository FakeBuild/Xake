## TODO
  * clean (phony actions)
  * multiple outputs
  * условное правило (функция вместо маски)
  * incremental builds
  * complete abstraction over artifact (do not use fileinfo, resolve files when started using project dir)
  * rules versioning
  * ресурсы (CPU и пр.) для управления очередностью
  * ...

## Tasks TODO
  * command-line tool
  * CSC resources
  * MSBuild task


## In progress
  * диагностика и лог (детально в файл, кратко на экран)
  * exception handling and reporting

## Done
 * имя файла-результата как аргумент (для оператора **>)
 * задача system
 * правило для группы файлов ("\*.rc" \*\*> fun f -> ...)
 * файл (filepath) с разными операциями
 * списки (fileset)
 * матчинг имен артефактов/файлов + каталоги
 * parameterized filesets (оператор для условной конкатенации списков ["a.cs"] &? debug ["debug.cs"])
 * два вида fileset - правила и вычисленный список
 * CPU affinity as a script option
 * xake exec parameters (number of threads, log file, verbosity)
 * параметризация сценария, сценарий как объект (main + rules)

## Thoughts
 * артефакты: файлы + виртуальные артефакты. Со вторыми проблемы:
 * файлсет (как список артефактов) теряет смысл
 * усложнение правила - как матчинг правил, так и описание действия

## References
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
  * [Shake functions reference](http://hackage.haskell.org/package/shake-0.11.4/docs/Development-Shake.html)