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
let MaskTests (m,t) = Path.matches m "" t

[<TestCase("../subd1/a.ss", @"C:\projects\Xake\bin\Debug\subd1", @"C:\projects\Xake\bin\Debug\subd1\../subd1/a.ss", ExpectedResult = true)>]
[<TestCase("subd2/a.ss", @"C:\projects\Xake\bin\Debug\subd1", @"C:\projects\Xake\bin\Debug\subd1\subd2/a.ss", ExpectedResult = true)>]

let MaskTests3(m,root,t) = Path.matches m root t


[<TestCase("c:\\*\\*.c", "c:\\abc\\def\\..\\a.c",  ExpectedResult = true)>]
[<TestCase("c:\\*.c", "c:\\abc\\..\\a.c",          ExpectedResult = true)>]

[<TestCase("c:\\abc\\..\\*.c", "c:\\a.c",          ExpectedResult = true)>]
[<TestCase("c:\\abc\\def\\..\\..\\*.c", "c:\\a.c", ExpectedResult = true)>]
[<TestCase("c:\\abc\\..\\..\\*.c", "c:\\a.c",      ExpectedResult = false)>]
[<TestCase("c:\\abc\\**\\..\\*.c", "c:\\a.c",      ExpectedResult = true)>]
[<TestCase("c:\\abc\\..\\*.c", "c:\\abc\\..\\a.c", ExpectedResult = true)>]

let MaskWithParent(m,t) = Path.matches m "" t

let omitNumberedGroups (groupName: string, _) = not <| System.Char.IsDigit groupName.[0]

[<TestCase("(arch:*)/(platform:*)/autoexec.(ext:*)", "x86/win/autoexec.bat", ExpectedResult = "arch-x86 platform-win ext-bat")>]
[<TestCase("(arch:*)-(platform:*)-autoexec.(ext:*)", "x86-win-autoexec.bat", ExpectedResult = "arch-x86 platform-win ext-bat")>]
[<TestCase("(filename:(arch:*)-(platform:*)-autoexec).(ext:*)", "x86-win-autoexec.bat", ExpectedResult = "filename-x86-win-autoexec arch-x86 platform-win ext-bat")>]
[<TestCase("(apl:*/*)/autoexec.*", "x86/win/autoexec.bat", ExpectedResult = "apl-x86/win")>]
[<TestCase("(path:bin/**)/file.o", "bin/x86/win/3.11wg/file.o", ExpectedResult = "path-bin/x86/win/3.11wg")>]

let MatchGroupsInRule(m,t) =
    Path.matchGroups m "" t
    |> function
    | Some list ->
        list
        |> List.filter omitNumberedGroups
        |> List.map (fun (n,v) -> sprintf "%s-%s" n v) 
        |> String.concat " "
    | None -> ""

[<TestCase("mlib.dll", "c:/mlib.dll", ExpectedResult = true)>]
[<TestCase("mli?.dll", "c:/mlib.dll", ExpectedResult = true)>]
[<TestCase("mli?.dll", "c:/mli.dll", ExpectedResult = false)>]
[<TestCase("*/mlib.dll", "c:/abc/mlib.dll", ExpectedResult = true)>]
[<TestCase("x86/win/mlib.*", "c:/x86/win/mlib.dll", ExpectedResult = true)>]
[<TestCase("*/*/mlib.*", "c:/x86/win/mlib.dll", ExpectedResult = true)>]
[<TestCase("*/*/mlib.*", "c:/123/x86/win/mlib.dll", ExpectedResult = false)>]

let MatchRule(m,t) =
    Path.matchGroups m "c:/" t
    |> Option.map (List.filter omitNumberedGroups) |> Option.isSome

[<TestCase("**/mlib.dll", "c:/abc/mlib.dll", ExpectedResult = true)>]
[<TestCase("**/mlib.dll", "c:/abc/def/mlib.dll", ExpectedResult = true)>]
[<TestCase("**/mlib.dll", "c:/mlib.dll", ExpectedResult = false)>]

let RecurseInRule(m,t) =
    Path.matchGroups m "c:/" t
    |> Option.map (List.filter omitNumberedGroups) |> Option.isSome
