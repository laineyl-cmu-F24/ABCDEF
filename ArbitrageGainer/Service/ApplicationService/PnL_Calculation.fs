module Service.ApplicationService.PnL
open System

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