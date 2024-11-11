module Service.ApplicationService.Cache

open Core.Model.Models
open Microsoft.FSharp.Collections

// Messages that the Cache Agent can process
type CacheMessage =
    | UpdateCache of Quote
    | GetQuote of string * AsyncReplyChannel<Option<Quote>>
    | PrintCache //remove later

let createCacheAgent () =
    let rec agentLoop (cache: Map<(string * string), Quote>) (inbox: MailboxProcessor<CacheMessage>) =
        async {
            let! msg = inbox.Receive()
            match msg with
            | UpdateCache quote ->
                // Update the cache with the new quote
                let key = (quote.Symbol, quote.Exchange)
                let updatedCache = cache.Add(key, quote)
                return! agentLoop updatedCache inbox
            | GetQuote (symbol, replyChannel) ->
                // Retrieve all quotes for the symbol across exchanges
                let quotes =
                    cache
                    |> Map.filter(fun (sym, _) _ -> sym = symbol)
                    |> Map.toList
                    |> List.map snd                
                match quotes with
                | [] ->
                    replyChannel.Reply(None)
                    return! agentLoop cache inbox
                | _ ->
                    // Return the latest quote
                    let latestQuote = quotes |> List.maxBy(fun q -> q.Timestamp)
                    replyChannel.Reply(Some latestQuote)
                    return! agentLoop cache inbox
            //remove later
            | PrintCache ->
                printfn "Cache size: %d" (Map.count cache)
                cache
                |> Map.iter(fun (symbol, exchange) quote ->
                    printfn "Symbol: %A, Exchange: %s, Bid: %f, Ask: %f, Timestamp: %O"
                        symbol exchange quote.BidPrice quote.AskPrice quote.Timestamp)
            return! agentLoop cache inbox
        }
    MailboxProcessor.Start(fun inbox -> agentLoop Map.empty inbox)
