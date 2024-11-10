module Core.Interfaces

open Core.Models

type IWebSocketClient =
    abstract member Connect : unit -> Async<DomainResult<unit>>
    abstract member SendMessage<'T> : 'T -> Async<DomainResult<unit>>
    abstract member ReceiveMessage : unit -> Async<DomainResult<string>>
    abstract member Close : unit -> Async<DomainResult<unit>>
    abstract member IsOpen: bool
