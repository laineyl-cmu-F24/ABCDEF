module M2.Core.Helper

let map f result =
    match result with
    | Ok value -> Ok (f value)
    | Error e -> Error e

