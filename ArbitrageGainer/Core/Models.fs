module Core.Models
open System

type TradeRecord = {
    Pair: string
    OpportunityCount: int
}

type Quote = {
    Symbol: string
    Exchange: string
    BidPrice: decimal
    AskPrice: decimal
    Timestamp: DateTime
}

type StatusMessage = {
    ev: string
    status: string
    message: string
}

type ParseResult =
    | QuoteReceived of Quote
    | StatusReceived of StatusMessage
    | ParseError of string
   
type DomainError =
    | ConnectionError of string
    | AuthenticationError of string
    | SubscriptionError of string
    | DataError of string

type DomainResult<'Success> = 
    | Ok of 'Success
    | Error of DomainError

type Message = {
    action: string
    params: string
}

