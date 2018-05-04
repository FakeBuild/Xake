let escape c s =
  match c,s with
  | '"',  (b, str) -> (true,  '\\' :: '\"' ::  str)
  | '\\', (true,  str) -> (true,  '\\' :: '\\' :: str)
  | '\\', (false, str) -> (false, '\\' :: str)
  | c, (b, str) -> (false, c :: str)

let translate (str:string) =
  let ca = str.ToCharArray()
  let res = Array.foldBack escape ca (true,[])
  System.String(res |> snd |> List.toArray)
  
let escapeArgs (str:string) =

  let escape c s =
    match c,s with
    | '"',  (b, str) -> (true,  '\\' :: '\"' ::  str)
    | '\\', (true,  str) -> (true,  '\\' :: '\\' :: str)
    | '\\', (false, str) -> (false, '\\' :: str)
    | c, (b, str) -> (false, c :: str)

  if str |> String.exists (fun c -> c = '"' || c = ' ') then
    let ca = str.ToCharArray()
    let res = Array.foldBack escape ca (true,['"'])
    "\"" + System.String(res |> snd |> List.toArray)
  else
    str

let escape2 c s =
  let (bb,ss) = s
  printf "%b %c %s\n" bb c (new System.String(ss |> List.toArray))
  match c,s with
  | '"',  (b, str) -> (true,  '\\' :: '\"' ::  str)
  | '\\', (true,  str) -> (true,  '\\' :: '\\' :: str)
  | '\\', (false, str) -> (false, '\\' :: str)
  | c, (b, str) -> (false, c :: str)

let translate2 (str:string) =
  let ca = str.ToCharArray()
  let res = Array.foldBack escape2 ca (true,['"']) |> snd |> List.rev
  "\"" + System.String(res |> List.toArray)
  


