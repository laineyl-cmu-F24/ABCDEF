module Core.Models
open System

type CryptoSymbol = CryptoSymbol of string
type ArbitrageOpportunityCount = ArbitrageOpportunityCount of int
type CryptoArbitrage = {
    Symbol: CryptoSymbol
    OpportunityCount: ArbitrageOpportunityCount
}

type Quote = {
    Symbol: CryptoSymbol
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


type TradingParameters = {
    NumOfCryptos: int
    MinPriceSpread: decimal
    MinTransactionProfit: decimal
    MaxTradingValue: decimal
    InitialInvestmentAmount: decimal
    Email: string
}

type Message = {
    action: string
    params: string
}