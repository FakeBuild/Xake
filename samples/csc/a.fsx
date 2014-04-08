// xake build file

#r @"..\..\bin\Xake.Core.dll"
open Xake

"main" *> fun _ -> rule {
  // abc\ \-
  do! cmd "ls" ["foo\"\"\"\"\"\"bar"; """ "abc\ " """] |> Async.Ignore
}

printfn "Building main"
run [ "main" ] |> ignore

////////////////////////////////////////////////////////////
(*

xake {DefaultOptions with Affinity = 4} {
  want ["main"]

  rule "main" *> fun _ -> rule {
    need ["version.cs"; "assemblyinfo.cs"]
    // abc\ \-
    do! cmd "ls " ["foo\"\"\"\"\"\"bar"; """ "abc\ "" \-" """] |> Async.Ignore
  }

  do onerror async {
  }

  do finally async {
  }
}

*)