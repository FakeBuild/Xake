namespace Xake

[<AutoOpen>]
module ResourceFileset =

    type ResourceSetOptions = {Prefix:string; DynamicsPrefix:bool}
    type ResourceFileset = ResourceFileset of ResourceSetOptions * Fileset

    let DefaultOptions = {ResourceSetOptions.Prefix = ""; DynamicsPrefix = false}
    let Empty = ResourceFileset (DefaultOptions,Fileset.Empty)

    module private Impl =

        let changePrefix value (ResourceFileset (opts,ff)) = ResourceFileset ({opts with Prefix = value}, ff)
        let changeDynamicPrefix value (ResourceFileset (opts,ff)) = ResourceFileset ({opts with DynamicsPrefix = value}, ff)
        let changeFileset value (ResourceFileset (opts,ff)) = ResourceFileset (opts, value)
        
    type ResourceFilesetBuilder() =
        inherit FilesetBuilder()

        [<CustomOperation("prefix")>]
        member this.Prefix(fs,prefix) = fs |> Impl.changePrefix prefix

        [<CustomOperation("dynamic")>]
        member this.DynamicPrefix(resset,d) = resset |> Impl.changeDynamicPrefix d

        [<CustomOperation("files")>]
        member this.ResourceFiles(resset,fs) = resset |> Impl.changeFileset fs

        member this.Yield(())    = Empty
        member this.Delay(f) = f()
        member this.Zero() = this.Yield ( () )

        member x.Return(a) = x.Yield(a)


    let resourceset = ResourceFilesetBuilder()
