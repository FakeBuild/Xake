#r "nuget: Xake, 1.1.4.427-beta"
open Xake

do xakeScript {

  phony "main" (recipe {
    do! trace Message "The exception thrown below will be silently ignored"
    failwith "some error"
    } |> WhenError ignore)

  // TODO more examples and tricks

}
