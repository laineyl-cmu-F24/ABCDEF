module api
open System
open Suave
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.RequestErrors
open System.Collections.Generic

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

type TradeRecord = {
    CurrencyPair: string
    BuyExchange: string
    SellExchange: string
    BuyPrice: decimal
    SellPrice: decimal
    Volume: decimal
    Timestamp: DateTime
}

type SystemState = {
    TradingParams: TradingParameters option
    IsTradingActive: bool
    TradeHistory: TradeRecord list
}

let initialState = {
    TradingParams = None
    IsTradingActive = false
    TradeHistory = []
}

let toDictionary (state: SystemState) =
    let dict = Dictionary<string, obj>()
    match state.TradingParams with
    | Some p ->
        dict.Add("NumOfCrypto", box p.NumOfCrypto)
        dict.Add("MinSpreadPrice", box p.MinSpreadPrice)
        dict.Add("MinTransactionProfit", box p.MinTransactionProfit)
        dict.Add("MaxTransactionValue", box p.MaxTransactionValue)
        dict.Add("MaxTradeValue", box p.MaxTradeValue)
        dict.Add("InitialInvestmentAmount", box p.InitialInvestmentAmount)
        dict.Add("Email", box p.Email)
        dict.Add("PnLThreshold", box p.PnLThreshold)
    | None -> ()
    dict

let handleRequest func state =
    fun (context: HttpContext) ->
        async {
            let! response, newState = func state context
            let webPart = response
            let newUserState = toDictionary newState
            return! webPart context |> Async.map (Option.map (fun ctx -> { ctx with userState = newUserState }))
        }
        
let setTradingParameters (state: SystemState) (context: HttpContext) =
    async {
        let req = context.request
        match req.formData "NumOfCrypto", req.formData "MinSpreadPrice", req.formData "MinTransactionProfit",
              req.formData "MaxTransactionValue", req.formData "MaxTradeValue", req.formData "InitialInvestmentAmount", 
              req.formData "Email", req.formData "PnLThreshold" with
        | Choice1Of2 numOfCrypto, Choice1Of2 minSpreadPrice, Choice1Of2 minTransactProfit, Choice1Of2 maxTransactVal,
          Choice1Of2 maxTradeVal, Choice1Of2 initialInvestment, Choice1Of2 email, Choice1Of2 pnlThreshold ->
            let parameters = {
                NumOfCrypto = int numOfCrypto
                MinSpreadPrice = decimal minSpreadPrice
                MinTransactionProfit =  decimal minTransactProfit
                MaxTransactionValue = decimal maxTransactVal
                MaxTradeValue = decimal maxTradeVal 
                InitialInvestmentAmount = decimal initialInvestment
                Email = match email with
                        | "" -> None
                        | _ -> Some(email)
                PnLThreshold = match pnlThreshold with
                               | "" -> None
                               | _ -> Some(decimal pnlThreshold)
            }
            let updatedState = { state with TradingParams = Some parameters }
            printfn $"Updated State: %A{updatedState}"
            return (Successful.OK "Trading parameters updated successfully", updatedState)
        | _ -> return (RequestErrors.BAD_REQUEST "Invalid parameters provided", state)
    }

let app initialState =
    choose [
        POST >=> path "/api/strategy" >=> handleRequest setTradingParameters initialState
        // GET >=> path "/api/strategy" >=> handleRequest getTradingParameters initialState
        // GET >=> path "/api/currencies/cross-traded" >=> handleRequest identifyCrossTradedPairs initialState
        // POST >=> path "/api/arbitrage/historical" >=> request (handleRequest getHistoricalArbitrage initialState)
        // POST >=> path "/api/trading-launch" >=> handleRequest startTrading initialState
        // POST >=> path "/api/trading-stop" >=> handleRequest stopTrading initialState
        // GET >=> path "/api/profit-loss" >=> handleRequest calculatePnL initialState
        // GET >=> path "/api/profit-loss/threshold-check" >=> handleRequest checkPnLThreshold initialState
    ]
    
startWebServer defaultConfig (app initialState)