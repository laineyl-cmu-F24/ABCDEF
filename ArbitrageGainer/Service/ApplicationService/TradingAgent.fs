module Service.ApplicationService.TradingAgent

open Core.Model.Models
open System
open Service.ApplicationService.Cache


let findArbitrageOpportunities (cachedQuotes:seq<CachedQuote>) (tradingParams: TradingParameters) =
    let groupedQuote =
        cachedQuotes
        |> Seq.groupBy(fun q -> q.Quote.Pair)
        |> Seq.map(fun(pair, quotes) -> (pair, quotes |> Seq.toList))
        
    groupedQuote
    |> Seq.choose(fun(pair, quotes) ->
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
    |> Seq.tryHead

let placeOrder orderType exchange pair price quantity =
    async {
        printfn "%s, %s, %s %M %M" pair exchange orderType price quantity
    }
let executeTrades (opportunity:ArbitrageOpportunity) (tradingParams:TradingParameters) =
    async {
        let tradeKey = sprintf "%s_%s_%s_%M" opportunity.Pair opportunity.BuyCachedQuote.Quote.Exchange opportunity.SellCachedQuote.Quote.Exchange opportunity.Spread
        printfn "Attempting to execute trade with key: %s" tradeKey
        let buyQty = opportunity.BuyCachedQuote.RemainingAskSize
        let sellQty = opportunity.SellCachedQuote.RemainingBidSize
        let maxPossibleQty = min buyQty sellQty
        
        let maxQtyByTrx = tradingParams.MaxTransactionValue / opportunity.BuyCachedQuote.Quote.AskPrice
        let maxQtyByTrading = tradingParams.MaxTradeValue / opportunity.BuyCachedQuote.Quote.AskPrice
        
        let maxQty = [maxPossibleQty; maxQtyByTrx; maxQtyByTrading] |> List.min
        
        let potentialProfit = maxQty * opportunity.Spread
        match potentialProfit >= tradingParams.MinTransactionProfit with
        |true ->
            printfn "Executing arbitrage opportunity for %s" opportunity.Pair
            //printfn "Buying %M units at %M from exchange %s" maxQty opportunity.BuyQuote.AskPrice opportunity.BuyQuote.Exchange
            //printfn "Selling %M units at %M to exchange %s" maxQty opportunity.SellQuote.BidPrice opportunity.SellQuote.Exchange
            do! placeOrder "Buy" opportunity.BuyCachedQuote.Quote.Exchange opportunity.Pair opportunity.BuyCachedQuote.Quote.AskPrice maxQty
            do! placeOrder "Sell" opportunity.SellCachedQuote.Quote.Exchange opportunity.Pair opportunity.SellCachedQuote.Quote.BidPrice maxQty
        |false -> () 
    }
   
let processArbitrageOpportunities (cacheAgent: MailboxProcessor<CacheMessage>) (tradingParams: TradingParameters) =
    async {
        let! currentMarketData = cacheAgent.PostAndAsyncReply(GetAllQuotes)
        let arbitrageOpportunities = findArbitrageOpportunities currentMarketData tradingParams
        
        match arbitrageOpportunities with
        |Some opportunity ->
            do! executeTrades opportunity tradingParams
        | None -> () 
    }
    

    

