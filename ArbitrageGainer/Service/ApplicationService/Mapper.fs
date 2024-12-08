module ArbitrageGainer.Service.ApplicationService.Mapper
open FSharp.Data
open System

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

let mapHistoricalData lines =
    lines
    |> Seq.collect(fun line ->
        match line |> String.IsNullOrWhiteSpace with
        | true -> Seq.empty
        | false ->
            let historicalData = HistoricalData.Parse(line)
            historicalData
            |> Seq.map(fun q -> 
                let bucket = q.T / 5L
                let key = $"{q.Pair}:{bucket}"
                let value = $"{q.X},{q.Bp},{q.Ap}"
                key, value))
    |> Seq.iter (fun (key, value) -> printfn "%s\t%s" key value)

let processMapper() =
    let input = Seq.initInfinite (fun _ -> Console.ReadLine())
    input
    |> Seq.takeWhile ((<>) null)
    |> mapHistoricalData