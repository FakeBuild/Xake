## TODO
  * параметризация сценария, сценарий как объект (main + rules)
  * правило для группы файлов ("\*.rc" \*\*> fun f -> ...)
  * ---
  * файл (filepath) с разными операциями
  * списки (fileset)
  * multiple outputs
  * матчинг имен артефактов/файлов + каталоги
  * условное правило (функция вместо маски)
  * incremental builds
  * clean
  * rules versioning
  * xake exec parameters (number of threads, log file, verbosity)
  * exception handling and reporting
  * parameterized filesets (оператор для условной конкатенации списков ["a.cs"] &? debug ["debug.cs"])
  * ...

## Done
 * имя файла-результата как аргумент (для оператора **>)
 * задача system

## Thoughts
 * артефакты: файлы + виртуальные артефакты. Со вторыми проблемы:
  * файлсет (как список артефактов) теряет смысл
  * усложнение правила - как матчинг правил, так и описание действия


## References
  * [Shake manual](https://github.com/ndmitchell/shake/blob/master/docs/Manual.md)
  * [Shake functions reference](http://hackage.haskell.org/package/shake-0.11.4/docs/Development-Shake.html)