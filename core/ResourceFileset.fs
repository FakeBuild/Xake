namespace Xake

[<AutoOpen>]
module ResourceFileset =

    type ResourceSetOptions = {Prefix:string option; DynamicPrefix:bool}
    type ResourceFileset = ResourceFileset of ResourceSetOptions * Fileset

    let DefaultOptions = {ResourceSetOptions.Prefix = None; DynamicPrefix = true}
    let Empty = ResourceFileset (DefaultOptions,Fileset.Empty)

    module private Impl =

        let changePrefix value (ResourceFileset (opts,ff)) = ResourceFileset ({opts with Prefix = value}, ff)
        let changeDynamicPrefix value (ResourceFileset (opts,ff)) = ResourceFileset ({opts with DynamicPrefix = value}, ff)
        let changeFileset value (ResourceFileset (opts,ff)) = ResourceFileset (opts, value)
        
    type ResourceFilesetBuilder() =

        [<CustomOperation("prefix")>]      member this.Prefix(fs,prefix) = fs |> Impl.changePrefix (Some prefix)
        [<CustomOperation("dynamic")>]     member this.DynamicPrefix(resset,d) = resset |> Impl.changeDynamicPrefix d
        [<CustomOperation("files")>]       member this.ResourceFiles(resset,fs) = resset |> Impl.changeFileset fs

        // the following methods duplicate fileset operations
        [<CustomOperation("basedir")>]     member this.BaseDir (ResourceFileset(opts,fs), (value:string)) = ResourceFileset (opts, fs @@ value)
        [<CustomOperation("includes")>]    member this.Includes(ResourceFileset(opts,fs), value) = ResourceFileset (opts, fs ++ value)
        [<CustomOperation("excludes")>]    member this.Excludes(ResourceFileset(opts,fs), value) = ResourceFileset (opts, fs -- value)

        member this.Yield(())   = Empty
        member this.Delay(f)    = f()
        member this.Zero()      = this.Yield ( () )

        member x.Return(a) = x.Yield(a)


    let resourceset = ResourceFilesetBuilder()
