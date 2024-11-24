module Service.ApplicationService.Metric

open System
open Core.Model.Models

type TradingParameters = {
    NumOfCrypto: int
    MinSpreadPrice: decimal
    MinTransactionProfit: decimal
    MaxTransactionValue: decimal
    MaxTradeValue: decimal
    InitialInvestmentAmount: decimal
    Email: string option
    PnLThreshold: decimal option
}


let validateInitialAmount (amount:decimal) =
    match amount <= 0M with
    | false -> Ok amount
    | _ -> Error (ValidationError NegativeInitialInvestment)

let validateTimeInterval (startTime:int64) (endTime:int64) =
    match endTime <= startTime with
    | false -> Ok (startTime, endTime)
    | true ->  Error (ValidationError InvalidTimeRange)

let calculateAnnualizedMetric (startTime: int64) (endTime: int64) (PL: decimal) (initialInvestment: decimal) =
    let durationInYear = float (endTime - startTime) / 3.154e+10
    let percentageGrowth = float (PL / initialInvestment)
    (decimal (System.Math.Pow(percentageGrowth, 1.0 / durationInYear))) - 1M
    // printfn $"Requested annual: %A{result}"
    // result

let rec AnnualizedMetric initialAmount startTradingTime =
    match startTradingTime with
    | None -> Error (ValidationError InvalidTimeRange) // Handle case where trading hasn't started
    | Some startTime -> 
        let endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() 
        let pl = 100M 
        match validateInitialAmount initialAmount with
        | Error e -> Error e
        | Ok validAmount -> 
            match validateTimeInterval startTime endTime with
            | Error e -> Error e
            | Ok (validStart, validEnd) ->
                Ok (calculateAnnualizedMetric validStart validEnd pl validAmount)