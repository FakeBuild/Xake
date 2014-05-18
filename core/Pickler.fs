namespace Xake

open System

/// Pickler Combinators implementation
module Pickler = 

  type OutState = System.IO.BinaryWriter
  type InState = System.IO.BinaryReader

  /// Main pickler type
  type PU<'a> = { pickle: 'a -> OutState -> unit; unpickle:  InState -> 'a }
  
  /// Unit pickler, does nothing
  let unitPU = {pickle = (fun () _ -> ());   unpickle = fun _ -> ()}
  let idPU pu : PU<'a> = id pu

  /// Translates pickler of one type into another's
  let wrapPU (d:'a -> 'b, r: 'b -> 'a) (pu: PU<'a>) = {pickle = r >> pu.pickle; unpickle = pu.unpickle >> d}

  let bytePU  = {pickle = (fun (b:byte) st -> st.Write(b));   unpickle = fun st -> st.ReadByte()}
  let intPU   = {pickle = (fun (i:Int32) st -> st.Write(i));  unpickle = fun st -> st.ReadInt32()}
  let int64PU = {pickle = (fun (i:Int64) st -> st.Write(i));  unpickle = fun st -> st.ReadInt64()}
  let strPU   = {pickle = (fun (s:string) st -> st.Write(s)); unpickle = fun st -> st.ReadString()}
  let floatPU   = {pickle = (fun (s:single) st -> st.Write(s)); unpickle = fun st -> st.ReadSingle()}
  let doublePU   = {pickle = (fun (s:float) st -> st.Write(s)); unpickle = fun st -> st.ReadDouble()}

  let datePU = wrapPU (DateTime.FromBinary, fun (d:DateTime) -> d.Ticks) int64PU

  /// Tuple picklers
  let pairPU pu1 pu2 = {
    pickle = (fun (a,b) st -> (pu1.pickle a st : unit); (pu2.pickle b st))
    unpickle = fun st -> pu1.unpickle st, pu2.unpickle st}
  let triplePU pu1 pu2 pu3 = {
    pickle = (fun (a,b,c) st -> (pu1.pickle a st : unit); (pu2.pickle a st : unit); (pu3.pickle b st))
    unpickle = fun st -> pu1.unpickle st, pu2.unpickle st, pu3.unpickle st}
    
  let quadPU pu1 pu2 pu3 pu4 =
    wrapPU ((fun ((a,b),(c,d)) -> (a,b,c,d)), fun(a,b,c,d) -> (a,b),(c,d)) <| pairPU (pairPU pu1 pu2) (pairPU pu3 pu4)

  let private mux3 (a,b,c) x = (a x : unit); (b x : unit); (c x : unit)
  let private mux2 (a,b) x = (a x : unit); (b x : unit)

  /// List pickler
  let listPU pu = 
    let rec listP f = function | [] -> bytePU.pickle 0uy | h :: t -> mux3 (bytePU.pickle 1uy, f h, listP f t)
    let rec listUim f acc st = match bytePU.unpickle st with | 0uy -> List.rev acc | 1uy -> listUim f (f st :: acc) st | n -> failwithf "listU: found number %d" n
    {
      pickle = listP pu.pickle
      unpickle = listUim pu.unpickle []
    }

  // Variant (discriminated union) pickler
  let altPU<'a> (ftag: 'a -> int) (puu: PU<'a> array): PU<'a> =
    {
      pickle = fun (a:'a) ->
        let tag = ftag a in
        mux2 (bytePU.pickle <| byte tag, puu.[tag].pickle a)
      unpickle = fun st ->
        let tag = int <| bytePU.unpickle st in
        (puu.[tag].unpickle st)
    }
