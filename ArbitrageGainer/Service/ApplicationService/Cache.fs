module Service.ApplicationService.Cache

open Core.Model.Models
open Microsoft.FSharp.Collections

// Messages that the Cache Agent can process

type CacheMessage =
    | UpdateCache of Quote
    | GetQuote of string * AsyncReplyChannel<Option<Quote>>
    | GetAllQuotes of AsyncReplyChannel<CachedQuote list>
    | UpdateQuantities of pair: string * exchangeId: string * deltaBidSize: decimal * deltaAskSize: decimal
    | PrintCache //remove later

let createCacheAgent () =
    MailboxProcessor<CacheMessage>.Start(fun inbox ->
        let rec loop (cache: Map<string, Map<string, CachedQuote>>) =
            async {
                let! msg = inbox.Receive()
                match msg with
                | UpdateCache quote ->
                    let cachedQuote = {
                        Quote = quote
                        RemainingBidSize = quote.BidSize
                        RemainingAskSize = quote.AskSize
                    }
                    let updatedExchangeQuotes =
                        match cache.TryFind quote.Pair with
                        | Some exchangeQuotes -> exchangeQuotes.Add(quote.Exchange, cachedQuote)
                        | None -> Map.ofList [(quote.Exchange, cachedQuote)]
                    printfn "Cache updated with quote for %s from exchange %s" quote.Pair quote.Exchange
                    let updatedCache = cache.Add(quote.Pair, updatedExchangeQuotes)
                    return! loop updatedCache
                | GetAllQuotes replyChannel ->
                    // Flatten the cache to a list of CachedQuotes
                    let allCachedQuotes =
                        cache
                        |> Map.toList
                        |> List.collect (fun (_, exchangeQuotes) -> exchangeQuotes |> Map.toList |> List.map snd)
                    replyChannel.Reply(allCachedQuotes)
                    return! loop cache
                | UpdateQuantities (pair, exchangeId, deltaBidSize, deltaAskSize) ->
                    // Update the remaining quantities after a trade
                    let updatedCache =
                        cache |> Map.change pair (fun exchangeQuotesOpt ->
                            exchangeQuotesOpt |> Option.map (fun exchangeQuotes ->
                                exchangeQuotes |> Map.change exchangeId (fun cachedQuoteOpt ->
                                    cachedQuoteOpt |> Option.map (fun cachedQuote ->
                                        {
                                            cachedQuote with
                                                RemainingBidSize = cachedQuote.RemainingBidSize + deltaBidSize
                                                RemainingAskSize = cachedQuote.RemainingAskSize + deltaAskSize
                                        }))))
                    return! loop updatedCache
                | PrintCache ->
                    // For debugging purposes
                    printfn "Current Cache:"
                    cache |> Map.iter (fun key exchangeQuotes ->
                        printfn "Pair: %s" key
                        exchangeQuotes |> Map.iter (fun exchangeId cachedQuote ->
                            printfn "  Exchange: %s, Bid: %M (%M left), Ask: %M (%M left)"
                                exchangeId
                                cachedQuote.Quote.BidPrice
                                cachedQuote.RemainingBidSize
                                cachedQuote.Quote.AskPrice
                                cachedQuote.RemainingAskSize))
                    return! loop cache
            }
        loop Map.empty)