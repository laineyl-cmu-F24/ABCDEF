module M2.Core.Interfaces

open M2.Core.Models

type IWebSocketClient =
    abstract member Connect : unit -> Async<Result<unit, DomainError>>
    abstract member SendMessage<'T> : 'T -> Async<Result<unit, DomainError>>
    abstract member ReceiveMessage : unit -> Async<Result<string, DomainError>>
    abstract member Close : unit -> Async<Result<unit, DomainError>>
