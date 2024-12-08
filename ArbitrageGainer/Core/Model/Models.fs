module Core.Model.Models
open System
open MongoDB.Driver
open MongoDB.Bson
open FSharp.Data

type PnLEvent =
    | ThresholdExceeded
    | TradingStopped

let pnLEvent = Event<PnLEvent>()
let onPnLEvent = pnLEvent.Publish

type TradeRecord = {
    Id: ObjectId
    Pair: string
    OpportunityCount: int
}

type Exchange =
    | Bitfinex
    | Kraken
    | Bitstamp


type Quote = {
    Symbol: string
    Pair: string
    Exchange: Exchange
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

//type ArbitrageOpportunity = {
  //  Symbol: string
  //  BuyExchange: Exchange
  //  SellExchange: Exchange
  //  BuyPrice: decimal
  //  SellPrice: decimal
  //  AvailableAmount: decimal
//}

type StatusMessage = {
    ev: string
    status: string
    message: string
}

type ParseSuccessResult =
    | QuoteReceived of Quote
    | StatusReceived of StatusMessage
    
type ValidationError = 
    | NegativeInitialInvestment
    | InvalidTimeRange
    
type DomainError =
    | ConnectionError of string
    | AuthenticationError of string
    | SubscriptionError of string
    | DataError of string
    | DatabaseError of string
    | ParseError of string
    | ValidationError of ValidationError


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

type HistoricalArbitrageOpportunity = {
    Pair: string
    Opportunity: int
}
    
type Side = Buy | Sell

// Helper functions to convert between Side and string
let sideToString (side: Side) =
    match side with
    | Buy -> "Buy"
    | Sell -> "Sell"
    
let stringToSide (str: string) =
    match str with
    | "Buy" -> Buy
    | "Sell" -> Sell
    | _ -> failwith "Invalid side value"


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

type TransactionDB = {
    Id: string
    OrderId: string
    Exchange: string
    Symbol: string
    Side: string
    Price: decimal
    Amount: decimal
    Timestamp: DateTime
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
    nonce: int
}

type KrakenSubmitOrderResult = {
    txid: string list
}

type KrakenSubmitOrderResponse = {
    error: string list
    result: KrakenSubmitOrderResult
}

type KrakenRetrieveOrderTradesResult = {
    vol_exec: string 
}

type KrakenRetrieveOrderTradesResponse = {
    error: string list
    result: Map<string, KrakenRetrieveOrderTradesResult>
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