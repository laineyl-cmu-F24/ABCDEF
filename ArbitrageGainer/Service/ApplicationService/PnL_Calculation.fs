module Service.ApplicationService.PnL
open System

type Side =
    | Buy
    | Sell

type Exchange =
    | Exchange of string

type Transaction = {
    Id: string
    OrderId: string
    Exchange: Exchange
    ExchangeSymbol: string
    Side: Side
    Price: decimal
    Amount: decimal
    decimalTimestamp: DateTime
}

// calculate the PL of an order
let calculatePLofTransaction (order: Transaction) = None
  