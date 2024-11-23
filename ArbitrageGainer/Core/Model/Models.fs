module Core.Model.Models
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
    | DatabaseError of string
    | PairParseError of string


type DomainResult<'Success> = 
    | Ok of 'Success
    | Error of DomainError

type Message = {
    action: string
    params: string
}

type Exchange =
    | Bitfinex
    | Kraken
    | Bitstamp

type Side = Buy | Sell

type Order = {
    Id: string
    Exchange: Exchange
    Symbol: string
    Side: Side
    Price: decimal
    Amount: decimal
    RemainingAmount: decimal
    OrderId: string
    Timestamp: DateTime
}

type OrderStatus = {
    OrderId: string
    FulfilledAmount: decimal
    RemainingAmount: decimal
    Status: string
}

type Transaction = {
    Id: string
    OrderId: string
    Exchange: Exchange
    Symbol: string
    Side: Side
    Price: decimal
    Amount: decimal
    Timestamp: DateTime
}

type ArbitrageOpportunity = {
    Symbol: string
    BuyExchange: Exchange
    SellExchange: Exchange
    BuyPrice: decimal
    SellPrice: decimal
    AvailableAmount: decimal
}

type BitfinexSubmitOrderRequest = {
    symbol: string
    amount: decimal
    price: decimal
    exchange: string
    side: string
    ``type``: string
}

type BitfinexSubmitOrderResponse = {
    id: string
    symbol: string
    exchange: string
    price: decimal
    avg_execution_price: decimal
    side: string
    ``type``: string
    timestamp: string
    is_live: bool
    is_cancelled: bool
    is_hidden: bool
    was_forced: bool
    original_amount: decimal
    remaining_amount: decimal
    executed_amount: decimal
}

type BitfinexRetrieveOrderTradesRequest = {
    order_id: string
}

type BitfinexRetrieveOrderTradesResponse = {
    id: string
    symbol: string
    exchange: string
    price: decimal
    amount: decimal
    timestamp: string
    side: string
    ``type``: string
}

type KrakenSubmitOrderRequest = {
    pair: string
    ``type``: string
    ordertype: string
    volume: decimal
    price: decimal
}

type KrakenSubmitOrderResponse = {
    txid: string list
}

type KrakenRetrieveOrderTradesResponse = {
    id: string
    symbol: string
    exchange: string
    price: decimal
    amount: decimal
    timestamp: string
    side: string
    ``type``: string
}

type BitstampEmitOrderRequest = {
    amount: decimal
    price: decimal
}

type BitstampEmitOrderResponse = {
    id: string
    datetime: string
    currency_pair: string
    ``type``: string
    amount: decimal
    price: decimal
    status: string
}

type BitstampRetrieveOrderStatusResponse = {
    id: string
    datetime: string
    currency_pair: string
    ``type``: string
    amount: decimal
    price: decimal
    status: string
}