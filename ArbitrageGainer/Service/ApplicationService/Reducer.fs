module ArbitrageGainer.Service.ApplicationService.Reducer

open System

let reduceHistoricalData historicalData =
    historicalData
    |> Seq.groupBy fst
    |> Seq.map (fun (key, values) ->
        let quotes =
            values
            |> Seq.map snd
            |> Seq.map (fun (value:String) ->
                let parts = value.Split(',')
                let exchangeID = int parts.[0]
                let bid = decimal parts.[1]
                let ask = decimal parts.[2]
                (exchangeID, bid, ask))
            
        let highestBids =
            quotes
            |> Seq.groupBy(fun (exchangeID, _, _) -> exchangeID)
            |> Seq.map(fun (_, group) -> group |> Seq.maxBy (fun (_, bid, _) -> bid))
        
        let lowestAsks =
            quotes
            |> Seq.groupBy (fun (exchangeID, _, _) -> exchangeID)
            |> Seq.map (fun (_, group) -> group |> Seq.minBy (fun (_, _, ask) -> ask))
            
        let opportunities =
            highestBids
            |> Seq.collect (fun (exchange1, bid, _) ->
                lowestAsks
                |> Seq.filter (fun (exchange2, _, ask) -> exchange1 <> exchange2 && bid - ask > 0.01m)
                |> Seq.map (fun _ -> 1))
            |> Seq.sum
        
        key, opportunities)
    |> Seq.filter(fun (_, opportunities) -> opportunities > 0)
    |> Seq.iter (fun (key, opportunities) -> printfn "%s\t%d" key opportunities)

let processReducer() =
    let input = Seq.initInfinite (fun _ -> Console.ReadLine())
    input
    |> Seq.takeWhile ((<>) null)
    |> Seq.map (fun line ->
        let parts = line.Split('\t')
        (parts.[0], parts.[1]))
    |> reduceHistoricalData
        
        
        
        

