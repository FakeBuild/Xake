module StateMonad
 
let (>>=) x f =
   (fun s0 ->
      let a,s = x s0
      f a s)      
 
let returnS a = (fun s -> a, s)
 
type StateBuilder() =
  member m.Bind(x, f) = x >>= f
  member m.Return a = returnS a
 
let state = new StateBuilder()
 
let getState = (fun s -> s, s)
let setState s = (fun _ -> (),s) 
let Execute m s = m s |> fst

let a = state {
  let! x = 1
  return x
}