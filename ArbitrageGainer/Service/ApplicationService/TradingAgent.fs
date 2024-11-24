module Service.ApplicationService.TradingAgent

open Core.Model.Models
open Service.ApplicationService.Cache

//check if a spread exists
let findArbitrageOpportunities (cachedQuotes:seq<CachedQuote>) (tradingParams: TradingParameters) =
    let groupedQuote =
        cachedQuotes
        |> Seq.groupBy(fun q -> q.Quote.Pair)
        |> Seq.map(fun(pair, quotes) -> (pair, quotes |> Seq.toList))
        
    groupedQuote
    |> Seq.choose(fun(pair, quotes) ->
        // Filter quotes with remaining sizes > 0
        let potentialBuys = quotes |> List.filter(fun q -> q.RemainingAskSize > 0M)
        let potentialSells = quotes |> List.filter(fun q -> q.RemainingBidSize > 0M)
        
        match potentialBuys.IsEmpty || potentialSells.IsEmpty with
        | true -> None
        | false ->
            let lowestAsk = potentialBuys |> List.minBy (fun q -> q.Quote.AskPrice)
            let highestBid = potentialSells |> List.maxBy (fun q -> q.Quote.BidPrice)
        
            let spread = highestBid.Quote.BidPrice - lowestAsk.Quote.AskPrice
        
            match spread >= tradingParams.MinSpreadPrice with
            |true ->
                Some {
                    Pair = pair
                    BuyCachedQuote = lowestAsk
                    SellCachedQuote = highestBid
                    Spread = spread
                }
            |false -> None
    )
    |> Seq.sortByDescending(fun opp -> opp.Spread)
    |> Seq.tryHead//take largest spread

let getExchangeName exchangeId =
    match exchangeId with
    | "2" -> "BitFinex"
    | "6" -> "BitStamp"
    | "23" -> "Kraken"
    | _ -> "Unknown Exchange"

let getExchangeString exchange = 
    match exchange with
    | Bitfinex -> "2"
    | Bitstamp -> "6"
    | Kraken -> "23"
    | _ -> "Unknown Exchange"

let placeOrder orderType exchange pair price quantity =
    async {
        let exchangeName = getExchangeName exchange
        printfn "%s, %A (%s), %s, %M, %M" pair exchange exchangeName orderType price quantity
    }

// Apply trading strategy
let executeTrades (opportunity: ArbitrageOpportunity) (tradingParams: TradingParameters) (cacheAgent: MailboxProcessor<CacheMessage>) =
    async {
        // Maximum possible quantity to trade
        let buyQty = opportunity.BuyCachedQuote.RemainingAskSize
        let sellQty = opportunity.SellCachedQuote.RemainingBidSize
        let maxPossibleQty = min buyQty sellQty

        // Maximum quantity allowed by tradingParams
        let maxQtyByTrx = tradingParams.MaxTransactionValue / opportunity.BuyCachedQuote.Quote.AskPrice
        let maxQtyByTrading = tradingParams.MaxTradeValue / opportunity.BuyCachedQuote.Quote.AskPrice
        let maxQty = [maxPossibleQty; maxQtyByTrx; maxQtyByTrading] |> List.min

        // Calculate profit
        let potentialProfit = maxQty * opportunity.Spread
        match potentialProfit >= tradingParams.MinTransactionProfit with
        | true ->
            printfn "Executing arbitrage opportunity for %s" opportunity.Pair
            // Emit buy and sell
            do! placeOrder "Buy" (getExchangeString opportunity.BuyCachedQuote.Quote.Exchange) opportunity.Pair opportunity.BuyCachedQuote.Quote.AskPrice maxQty
            do! placeOrder "Sell" (getExchangeString opportunity.SellCachedQuote.Quote.Exchange) opportunity.Pair opportunity.SellCachedQuote.Quote.BidPrice maxQty
            // Update remaining quantities
            cacheAgent.Post(UpdateQuantities(opportunity.Pair, opportunity.BuyCachedQuote.Quote.Exchange, 0M, -maxQty))
            cacheAgent.Post(UpdateQuantities(opportunity.Pair, opportunity.SellCachedQuote.Quote.Exchange, -maxQty, 0M))
        | false -> () 
    }

   
let processArbitrageOpportunities (cacheAgent: MailboxProcessor<CacheMessage>) (tradingParams: TradingParameters) =
    async {
        let! currentMarketData = cacheAgent.PostAndAsyncReply(GetAllQuotes)
        let arbitrageOpportunities = findArbitrageOpportunities currentMarketData tradingParams
        
        match arbitrageOpportunities with
        |Some opportunity ->
            do! executeTrades opportunity tradingParams cacheAgent
        | None -> () 
    }
    

    

