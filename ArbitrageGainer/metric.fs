module Metric

open System

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

type ValidationError = 
    | NegativeInitialInvestment
    | InvalidTimeRange

type ValidationResult<'Success> = 
    | Ok of 'Success
    | Error of ValidationError

let validateInitialAmount (amount:decimal):ValidationResult<decimal> =
    match amount <= 0M with
    | false -> Ok amount
    | _ -> Error NegativeInitialInvestment

let validateTimeInterval (startTime:int64) (endTime:int64) : ValidationResult<int64*int64> =
    match endTime <= startTime with
    | false -> Ok (startTime, endTime)
    | true ->  Error InvalidTimeRange

let calculateAnnualizedMetric (startTime: int64) (endTime: int64) (PL: decimal) (initialInvestment: decimal) =
    let durationInYear = float (endTime - startTime) / 3.154e+10
    let percentageGrowth = float (PL / initialInvestment)
    (decimal (System.Math.Pow(percentageGrowth, 1.0 / durationInYear))) - 1M

let rec AnnualizedMetric (tradingParameters: TradingParameters) = 
    let initialAmount = tradingParameters.InitialInvestmentAmount
    let startTime = 1729728000000L
    let endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() 
    let pl = 100M 
    match validateInitialAmount initialAmount with
    | Error e -> Error e
    | Ok amount -> 
        match validateTimeInterval startTime endTime with
        | Error e -> Error e
        | Ok (validStart, validEnd) ->
            Ok (calculateAnnualizedMetric validStart validEnd pl validAmount)