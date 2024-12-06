module Service.ApplicationService.OrderManagement

open System
open System.Threading.Tasks
open MongoDB.Bson
open MongoDB.Driver
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface
open Infrastructure.Client.ModuleAPI
open Service.ApplicationService.PnL
open Service.ApplicationService.TradingState
open Logging.Logger

let rec handleOrderStatus (order: Order) (orderStatus: OrderStatus) : Task = task {
        match orderStatus.Status with
         
        | "FullyFilled" ->
            let transaction = {
                Id = ObjectId.GenerateNewId().ToString()
                OrderId = orderStatus.OrderId
                Exchange = order.Exchange
                Symbol = order.Symbol
                Side = order.Side
                Price = order.Price
                Amount = orderStatus.FulfilledAmount
                Timestamp = DateTime.UtcNow
            }
            let result = saveTransaction transaction
            printfn $"Transaction stored: %A{transaction}"
            addTransaction transaction
            printfn "Transaction added to PNL"
        | "PartiallyFilled" ->
            let transaction = {
                Id = ObjectId.GenerateNewId().ToString()
                OrderId = orderStatus.OrderId
                Exchange = order.Exchange
                Symbol = order.Symbol
                Side = order.Side
                Price = order.Price
                Amount = orderStatus.FulfilledAmount
                Timestamp = DateTime.UtcNow
            }
            let result = saveTransaction transaction
            printfn $"Partial transaction stored: %A{transaction}"
            addTransaction transaction
            printfn "Transaction added to PNL"

            // Emit a new order for the remaining amount
            let newOrder = {
                Id = ObjectId.GenerateNewId().ToString()
                Exchange = order.Exchange
                Symbol = order.Symbol
                Side = order.Side
                Price = order.Price
                Amount = orderStatus.RemainingAmount
                RemainingAmount = orderStatus.RemainingAmount
                OrderId = ""
                Timestamp = DateTime.UtcNow
            }

            // Submit the new order based on the exchange
            let! submittedOrder =
                match order.Exchange with
                | Bitfinex ->
                    createLogger "New Order Emitted to Bitfinex"
                    submitBitfinexOrder newOrder
                | Kraken ->
                    createLogger "New Order Emitted to Kraken"
                    submitKrakenOrder newOrder
                | Bitstamp ->
                    createLogger "New Order Emitted to Bitstamp"
                    emitBitstampOrder newOrder
                    
            return ()
        | _ -> printfn "Email notify user"
}

let emitBuySellOrders (opportunity: ArbitrageOpportunity) = task {
    let buyQuote = opportunity.BuyCachedQuote
    let sellQuote = opportunity.SellCachedQuote

    let buyPrice = buyQuote.Quote.AskPrice
    let sellPrice = sellQuote.Quote.BidPrice

    let availableBuyAmount = buyQuote.RemainingAskSize
    let availableSellAmount = sellQuote.RemainingBidSize
    let desiredAmount = Math.Min(availableBuyAmount, availableSellAmount)
    
    let! currentState = getTradingState ()
    let MAX_TOTAL_TRANSACTION_VALUE =
        match currentState.TradingParams with
        | Some tp -> tp.MaxTransactionValue
        | None -> failwith "Trading parameters are not available."
    // let MAX_TOTAL_TRANSACTION_VALUE = decimal 2000.0

    // Ensure total transaction value does not exceed MAX_TOTAL_TRANSACTION_VALUE
    let buyTotal = desiredAmount * buyPrice
    let sellTotal = desiredAmount * sellPrice
    let totalTransactionValue = buyTotal + sellTotal

    let adjustedAmount =
        match totalTransactionValue > MAX_TOTAL_TRANSACTION_VALUE with
        | true ->
            // Adjust the amount to fit within the max total transaction value
            let allowedAmount = MAX_TOTAL_TRANSACTION_VALUE / (buyPrice + sellPrice)
            Math.Floor(allowedAmount * 10000m) / 10000m // Round down to 4 decimal places
        | _ ->
            desiredAmount

    // Ensure final amount respects available amounts on both exchanges
    let finalAmount = Math.Min(adjustedAmount, Math.Min(availableBuyAmount, availableSellAmount))

    // Create Buy Order
    let buyOrder = {
        Id = ObjectId.GenerateNewId().ToString()
        Exchange = buyQuote.Quote.Exchange
        Symbol = buyQuote.Quote.Symbol
        Side = Buy
        Price = buyPrice
        Amount = finalAmount
        RemainingAmount = finalAmount
        OrderId = ""
        Timestamp = DateTime.UtcNow
    }

    // Create Sell Order
    let sellOrder = {
        Id = ObjectId.GenerateNewId().ToString()
        Exchange = sellQuote.Quote.Exchange
        Symbol = sellQuote.Quote.Symbol
        Side = Sell
        Price = sellPrice
        Amount = finalAmount
        RemainingAmount = finalAmount
        OrderId = ""
        Timestamp = DateTime.UtcNow
    }

    // Submit Buy Order - Await the asynchronous operation
    let! submittedBuyOrder =
        match buyOrder.Exchange with
        | Bitfinex ->
            createLogger "Buy Order Submitted to Bitfinex"
            submitBitfinexOrder buyOrder
        | Kraken ->
            createLogger "Buy Order Submitted to Kraken"
            submitKrakenOrder buyOrder
        | Bitstamp ->
            createLogger "Buy Order Submitted to Bitstamp"
            emitBitstampOrder buyOrder

    printfn $"Submitted Buy Order: %A{submittedBuyOrder}"

    // Submit Sell Order - Await the asynchronous operation
    let! submittedSellOrder =
        match sellOrder.Exchange with
        | Bitfinex ->
            createLogger "Sell Order Submitted to Bitfinex"
            submitBitfinexOrder sellOrder
        | Kraken ->
            createLogger "Sell Order Submitted to Kraken"
            submitKrakenOrder sellOrder
        | Bitstamp ->
            createLogger "Sell Order Submitted to Bitstamp"
            emitBitstampOrder sellOrder

    printfn $"Submitted Sell Order: %A{submittedSellOrder}"

    // Return the tuple of Orders
    return (submittedBuyOrder, submittedSellOrder)
}
