module ``Path tests``

open Xake
open NUnit.Framework

[<TestCase("c:\\**\\*.*", "c:\\", ExpectedResult = false)>]
[<TestCase("c:\\**\\*.*", "",     ExpectedResult = false)>]
[<TestCase("", "aa",              ExpectedResult = false)>]
[<TestCase("c:\\**\\*.*", "c:\\a.c",   ExpectedResult = true)>]
[<TestCase("c:\\**\\", "c:\\a.c",      ExpectedResult = false)>]
[<TestCase("c:\\*\\?.c", "c:\\!\\a.c", ExpectedResult = true)>]
[<TestCase("c:\\*\\?.c*", "c:\\!\\a.c",ExpectedResult = true)>]

[<TestCase("c:\\a.c", "d:\\a.c",  ExpectedResult = false)>]
[<TestCase("c:\\a.c", "C:\\A.c",  ExpectedResult = true)>]
[<TestCase("c:\\*.c", "d:\\a.c",  ExpectedResult = false)>]
[<TestCase("c:\\*.c", "c:\\a.c",  ExpectedResult = true)>]
[<TestCase("c:\\[(]*[)].c", "c:\\(a).c",  ExpectedResult = true, Description = "We are are not smart enough yet, so want brackets escated")>]
[<TestCase("c:\\[(]*.c", "c:\\(a.c",  ExpectedResult = true)>]

[<TestCase("c:\\abc\\*.c", "c:\\a.c",           ExpectedResult = false)>]
[<TestCase("c:\\abc\\*.c", "c:\\def\\a.c",      ExpectedResult = false)>]
[<TestCase("c:\\abc\\*.c", "c:\\abc\\def\\a.c", ExpectedResult = false)>]
[<TestCase("c:\\abc\\*.c", "c:\\abc\\a.c",      ExpectedResult = true)>]

[<TestCase("c:\\*\\*.c", "c:\\abc\\a.c",        ExpectedResult = true)>]
[<TestCase("c:\\*\\*.c", "c:\\abc\\def\\a.c",   ExpectedResult = false)>]
[<TestCase("c:\\**\\*.c", "c:\\abc\\def\\a.c",  ExpectedResult = true)>]
[<TestCase("c:/abc/../def\\a.c", "c:\\def\\a.c", ExpectedResult = true)>]
[<TestCase("c:\\def\\a.c", "c:/abc/../def\\a.c", ExpectedResult = true)>]
let MaskTests (m,t) = Path.matches m "" t |> Option.isSome

[<TestCase("../subd1/a.ss", @"C:\projects\Xake\bin\Debug\subd1", @"C:\projects\Xake\bin\Debug\subd1\../subd1/a.ss", ExpectedResult = true)>]
[<TestCase("subd2/a.ss", @"C:\projects\Xake\bin\Debug\subd1", @"C:\projects\Xake\bin\Debug\subd1\subd2/a.ss", ExpectedResult = true)>]

let MaskTests3(m,root,t) = Path.matches m root t |> Option.isSome


[<TestCase("c:\\*\\*.c", "c:\\abc\\def\\..\\a.c",  ExpectedResult = true)>]
[<TestCase("c:\\*.c", "c:\\abc\\..\\a.c",          ExpectedResult = true)>]

[<TestCase("c:\\abc\\..\\*.c", "c:\\a.c",          ExpectedResult = true)>]
[<TestCase("c:\\abc\\def\\..\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
[<TestCase("c:\\abc\\..\\..\\*.c", "c:\\a.c",      ExpectedResult = false)>]
[<TestCase("c:\\abc\\**\\..\\*.c", "c:\\a.c",      ExpectedResult = true)>]
[<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedResult = true)>]

let MaskWithParent(m,t) = Path.matches m "" t |> Option.isSome

[<TestCase("(arch:*)/(platform:*)/autoexec.(ext:*)", "x86/win/autoexec.bat", ExpectedResult = "arch-x86 platform-win ext-bat")>]
[<TestCase("(arch:*)-(platform:*)-autoexec.(ext:*)", "x86-win-autoexec.bat", ExpectedResult = "arch-x86 platform-win ext-bat")>]
[<TestCase("(filename:(arch:*)-(platform:*)-autoexec).(ext:*)", "x86-win-autoexec.bat", ExpectedResult = "filename-x86-win-autoexec arch-x86 platform-win ext-bat")>]

let MatchGroups(m,t) =
    Path.matches m "" t
    |> function
    | Some list ->
        list
        |> List.map (fun (n,v) -> sprintf "%s-%s" n v) 
        |> String.concat " "
    | None -> ""
