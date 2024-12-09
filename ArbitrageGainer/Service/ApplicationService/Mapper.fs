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
    
let rec readLines()  =
    match Console.In.ReadLine() with
    | null -> ()
    | line when String.IsNullOrWhiteSpace(line) -> readLines ()
    | line ->
        let data = HistoricalData.Parse(line)
        data 
        |> Seq.iter (fun q ->
            let bucket = q.T / 5L
            let key = sprintf "%d|%s" bucket q.Pair
            let value = sprintf "%d,%M,%M" q.X q.Bp q.Ap
            printfn "%s\t%s" key value
        )
        readLines()

[<EntryPoint>]
let main argv =
    readLines ()
    0
            