module ArbitrageGainer.Service.ApplicationService.Reducer

open System

let flushCurrentKey (currentKey:String) entries =
    match entries with
    | [] -> ()
    | _ ->
        let getQuotesByExchange =
            entries
            |> Seq.groupBy(fun(exchangeID, _, _) -> exchangeID)
            |> Seq.map(fun(exchange, historicalData) ->
                let highestBid = historicalData |> Seq.maxBy(fun(_, bp, _) -> bp)
                let lowestAsk = historicalData |> Seq.minBy(fun(_, _, ap) -> ap)
                (exchange, highestBid, lowestAsk))
            |> Seq.toList
        
        let opportunities =
            getQuotesByExchange
            |> Seq.collect(fun (_,(_,bid,_),_) ->
                getQuotesByExchange
                |> Seq.filter(fun (_,_,(_,_,ask)) -> bid - ask > 0.01M)
                |> Seq.map(fun _-> 1))
            |> Seq.sum
        
        match opportunities with
        | 0 -> ()
        | _ ->
            let parts = currentKey.Split('|')
            match parts with
            | [|_; pair|] -> printfn "%s\t%d" pair opportunities
            | _ -> ()

let rec readLines (currentKey: String) entries =
    match Console.In.ReadLine() with
    | null ->
        // End of stream
        flushCurrentKey currentKey entries
    | line ->
        let parts = line.Split('\t')
        match parts with
        | [| key; vals |] ->
            let valParts = vals.Split(',')
            match valParts with
            | [|exchangeId; bp; ap|] ->
                let newKey = key
                let exchangeID = int exchangeId
                let bid = decimal bp
                let ask = decimal ap
                match currentKey with
                | "" ->
                    // First key encountered
                    readLines newKey [(exchangeID, bid, ask)]
                | _ when newKey = currentKey ->
                    // Same key, just add the entry
                    readLines currentKey ((exchangeID, bid, ask)::entries)
                | _ ->
                    // New key encountered, flush old entries first
                    flushCurrentKey currentKey entries
                    readLines newKey [(exchangeID, bid, ask)]
            | _ ->
                readLines currentKey entries
        | _ ->
            readLines currentKey entries

[<EntryPoint>]
let main argv =
    readLines "" []
    0
                    
