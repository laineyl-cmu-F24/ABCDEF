module Service.ApplicationService.Historical
open FSharp.Data
open System.IO

type HistoricalData =
    JsonProvider<
    Sample="""[
        {
            "ev":"XQ",
            "pair":"CHZ-USD",
            "lp":0,
            "ls":0,
            "bp":0.0771,
            "bs":41650.4,
            "ap":0.0773,
            "as":142883.4,
            "t":1690409119847,
            "x":1,
            "r":1690409119856
        }
    ]
    """>
  
type Quote = {
  ExchangeID: int
  CurrencyPair: string
  TimeStamp: int64
  bid: decimal
  ask: decimal
}

let loadHistoricalData file =
    let jsonFile = File.ReadAllText(file)
    let historicalData = HistoricalData.Parse(jsonFile)
    
    historicalData |> Seq.map(fun q -> {
                                  ExchangeID = q.X
                                  CurrencyPair = q.Pair
                                  TimeStamp = q.T
                                  bid = q.Bp
                                  ask = q.Ap
                                })
    
let mapHistoricalData historicalData =
    historicalData
    |> Seq.groupBy(fun q -> q.TimeStamp / 5L)
    |> Seq.map(fun (timestamp, quotes) ->
                        quotes
                        |> Seq.groupBy(fun q -> q.CurrencyPair)
                        |> Seq.map(fun (pair, historicalData) ->
                            let getQuotesByExchange =
                                historicalData
                                |> Seq.groupBy (fun q -> q.ExchangeID)
                                |> Seq.map (fun (exchange, historicalData) ->
                                    let highestBid = historicalData |> Seq.maxBy (fun q -> q.bid)
                                    let lowestAsk = historicalData |> Seq.minBy (fun q -> q.ask)
                                    (exchange, highestBid, lowestAsk))
                            (pair, getQuotesByExchange)))
    |> Seq.concat
    
let reduceHistoricalData historicalData =
    historicalData
    |> Seq.map (fun (pair, quotes) ->
        let opportunities =
            quotes
            |> Seq.collect (fun (exchange1, bid, _) ->
                quotes
                |> Seq.filter (fun (exchange2, _, _) -> exchange1 <> exchange2)
                |> Seq.filter (fun (_, _, ask) -> bid.bid - ask.ask > 0.01m)
                |> Seq.map (fun _ -> 1))
            |> Seq.sum
        (pair,
         match opportunities  with
            | opportunities when opportunities > 0 -> true
            | _ -> false ))
    |> Seq.filter (fun (_, opportunities) -> opportunities = true)
    |> Seq.groupBy fst
    |> Seq.map (fun (pair, opportunities) -> (pair, Seq.length opportunities))
    
let calculateHistoricalArbitrage file=
    let data = loadHistoricalData file
    let mapResult = mapHistoricalData data
    let reduceResult = reduceHistoricalData mapResult
    
    reduceResult |> Seq.iter(fun (pair, opportunities) -> printfn $"{pair}, {opportunities} opportunities")
    reduceResult
