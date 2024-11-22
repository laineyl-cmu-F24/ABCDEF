module Core.Model.Models
open System

type TradeRecord = {
    Pair: string
    OpportunityCount: int
}


type Quote = {
    Pair: string
    Exchange: string
    BidPrice: decimal
    BidSize: decimal
    AskPrice: decimal
    AskSize: decimal
    Timestamp: DateTime
}

type CachedQuote = {
    Quote: Quote
    RemainingBidSize: decimal
    RemainingAskSize: decimal
}

type ArbitrageOpportunity = {
    Pair: string
    BuyCachedQuote: CachedQuote
    SellCachedQuote: CachedQuote
    Spread: decimal
}

type Order = {
    Pair: string
    Exchange: int
    Side: string
    Price: decimal
    Quantity: decimal
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
    | DatabaseError of string
    | PairParseError of string


type DomainResult<'Success> = 
    | Ok of 'Success
    | Error of DomainError

type Message = {
    action: string
    params: string
}

type TradingParameters = {
    NumOfCrypto: int
    MinSpreadPrice: decimal
    MinTransactionProfit: decimal
    MaxTransactionValue: decimal
    MaxTradeValue: decimal
    InitialInvestmentAmount: decimal
    Email: string option
    PnLThreshold: decimal option
}