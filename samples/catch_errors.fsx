#r "nuget: Xake, 2.0.0"
open Xake

do xakeScript {

  phony "main" (recipe {
    do! trace Message "The exception thrown below will be silently ignored"
    failwith "some error"
    } |> WhenError ignore)

  // TODO more examples and tricks

}
