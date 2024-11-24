module Service.ApplicationService.OrderManagement

open System
open System.Threading.Tasks
open MongoDB.Bson
open MongoDB.Driver
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface
open Infrastructure.Client.ModuleAPI

// TODO: wait to be called by data feed
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
                    submitBitfinexOrder newOrder
                | Kraken ->
                    submitKrakenOrder newOrder
                | Bitstamp ->
                    emitBitstampOrder newOrder
                    
            return ()
        | _ -> printfn "Email notify user"
}

let emitBuySellOrders (opportunity: ArbitrageOpportunity) =
        let desiredAmount = opportunity.AvailableAmount

        // Configuration Constants
        let MAX_TOTAL_TRANSACTION_VALUE = decimal 2000.0

        // Ensure total transaction value does not exceed MAX_TOTAL_TRANSACTION_VALUE
        let buyTotal = desiredAmount * opportunity.BuyPrice
        let sellTotal = desiredAmount * opportunity.SellPrice
        let totalTransactionValue = buyTotal + sellTotal

        let adjustedAmount =
            if totalTransactionValue > MAX_TOTAL_TRANSACTION_VALUE then
                // Adjust the amount to fit within the max total transaction value
                let allowedAmount = MAX_TOTAL_TRANSACTION_VALUE / (opportunity.BuyPrice + opportunity.SellPrice)
                Math.Floor(allowedAmount * 10000m) / 10000m // Round down to 4 decimal places
            else
                desiredAmount

        // Ensure buy order quantity does not exceed the ask quantity (assumed to be AvailableAmount)
        let finalAmount = Math.Min(adjustedAmount, opportunity.AvailableAmount)

        // Create Buy Order
        let buyOrder = {
            Id = ObjectId.GenerateNewId().ToString()
            Exchange = opportunity.BuyExchange
            Symbol = opportunity.Symbol
            Side = Buy
            Price = opportunity.BuyPrice
            Amount = finalAmount
            RemainingAmount = finalAmount
            OrderId = ""
            Timestamp = DateTime.UtcNow
        }

        // Create Sell Order
        let sellOrder = {
            Id = ObjectId.GenerateNewId().ToString()
            Exchange = opportunity.SellExchange
            Symbol = opportunity.Symbol
            Side = Sell
            Price = opportunity.SellPrice
            Amount = finalAmount
            RemainingAmount = finalAmount
            OrderId = ""
            Timestamp = DateTime.UtcNow
        }

        // Submit Buy Order
        let submittedBuyOrder =
            match buyOrder.Exchange with
            | Bitfinex ->
                submitBitfinexOrder buyOrder
            | Kraken ->
                submitKrakenOrder buyOrder
            | Bitstamp ->
                emitBitstampOrder buyOrder
        
        printfn $"%A{submittedBuyOrder}"

        // Submit Sell Order
        let submittedSellOrder =
            match sellOrder.Exchange with
            | Bitfinex ->
                submitBitfinexOrder sellOrder
            | Kraken ->
                submitKrakenOrder sellOrder
            | Bitstamp ->
                emitBitstampOrder sellOrder
                
        printfn $"%A{submittedSellOrder}"