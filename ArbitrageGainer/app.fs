module app
open System
open System.IO
open Suave
open Suave.Operators
open Suave.Filters
open MongoDB.Driver

open Service.ApplicationService.Historical
open Service.ApplicationService.Metric
open Service.ApplicationService.CrossTradedCurrencyPair
open Service.ApplicationService.PnL
open Service.ApplicationService.TradingState
open Core.Model.Models
open Service.ApplicationService.Toggle
open Logging.Logger


type SystemState = {
    TradingParams: TradingParameters option
    IsTradingActive: bool
    TradeHistory: TradeRecord list
    WebSocketClientCloseFunc: Option<unit -> Async<DomainResult<unit>>>
    StartTradingTime: int64 option
}

type AgentMessage =
    | SetTradingParameters of TradingParameters * AsyncReplyChannel<SystemState>
    | GetCurrentState of AsyncReplyChannel<SystemState>
    | GetTradeHistory of TradeRecord list * AsyncReplyChannel<SystemState>
    | ToggleTrading of bool * Option<unit -> Async<DomainResult<unit>>> * AsyncReplyChannel<SystemState>
    
    
let initialState = {
    TradingParams = None
    IsTradingActive = false
    TradeHistory = []
    WebSocketClientCloseFunc =  None
    StartTradingTime = None
}

let handleRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func context
            return! response context
        }
        
let handleEmptyRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func ()
            return! response context
        }

let setTradingParameters  (context: HttpContext) =
    async {
        let req = context.request
        let! initialState = getTradingParameters ()
        match req.formData "NumOfCrypto", req.formData "MinSpreadPrice", req.formData "MinTransactionProfit",
              req.formData "MaxTransactionValue", req.formData "MaxTradeValue", req.formData "InitialInvestmentAmount", 
              req.formData "Email", req.formData "PnLThreshold" with
        | Choice1Of2 numOfCrypto, Choice1Of2 minSpreadPrice, Choice1Of2 minTransactProfit, Choice1Of2 maxTransactVal,
          Choice1Of2 maxTradeVal, Choice1Of2 initialInvestment, Choice1Of2 email, Choice1Of2 pnlThreshold ->
            let parameters = {
                NumOfCrypto = int numOfCrypto
                MinSpreadPrice = decimal minSpreadPrice
                MinTransactionProfit = decimal minTransactProfit
                MaxTransactionValue = decimal maxTransactVal
                MaxTradeValue = decimal maxTradeVal 
                InitialInvestmentAmount = decimal initialInvestment
                Email = match email with
                            | null | "" -> None
                            | _ -> Some email
                PnLThreshold = match pnlThreshold with
                                   | null | "" -> None
                                   | _ -> Some (decimal pnlThreshold)
                }
            setTradingParameters parameters
            let! updatedState = getTradingParameters ()
            // printfn $"Updated State: %A{updatedState}"
            return (Successful.OK "Trading parameters updated successfully\n", updatedState)
        | _ -> return (RequestErrors.BAD_REQUEST "Invalid parameters provided\n", initialState)
    }
    
let getTradingParameters  (context: HttpContext) =
    async {
        let! currentParam = getTradingParameters ()
        match currentParam with
        | Some tradingParams ->
            let json = System.Text.Json.JsonSerializer.Serialize(tradingParams)
            return (Successful.OK json, ())
        | None ->
            return (RequestErrors.NOT_FOUND "Error when getting trading parameters\n", ())
    }
    
    
let getHistoricalArbitrage (context: HttpContext) =
    async {
        let logger = createLogger "historicalArbitrageLog.txt"
        logger "Historical Arbitrage Analysis: Start"
        let startTimestamp = DateTime.UtcNow
        let req = context.request
        match req.formData "file" with
        | Choice1Of2 filePath ->
            // printfn $"Requested file path: %s{filePath}"
            match filePath with
            | filePath when File.Exists filePath ->
                try
                    let result = calculateHistoricalArbitrage filePath
                    let tradeRecords =
                        result
                        |> Seq.map (fun (pair, opportunityCount) ->
                            { Pair =  pair; OpportunityCount = opportunityCount }
                        )
                        |> Seq.toList
                    let history = SetTradeHistory (tradeRecords)
                    // let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> GetTradeHistory(tradeRecords, reply))
                    let endTimestamp = DateTime.UtcNow
                    logger $"Historical Arbitrage Analysis: End - Time Taken: {endTimestamp - startTimestamp}"
                    return (Successful.OK "Success\n", $"Got historical arbitrage %A{result}")
                with
                | ex ->
                    let endTimestamp = DateTime.UtcNow
                    logger $"Historical Arbitrage Analysis: End - Invalid Input - Time Taken: {endTimestamp - startTimestamp}"
                    return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get historical arbitrage: %s{ex.Message}")
            | _ ->
                let endTimestamp = DateTime.UtcNow
                logger $"Historical Arbitrage Analysis: End - URL Not Found - Time Taken: {endTimestamp - startTimestamp}"
                return (RequestErrors.NOT_FOUND "Error\n", "File not found")
        | _ ->
            let endTimestamp = DateTime.UtcNow
            logger $"Historical Arbitrage Analysis: End - Bad Request - Time Taken: {endTimestamp - startTimestamp}"
            return (RequestErrors.BAD_REQUEST "Error\n", "No file path input")
    }
    
let getCrossTradeCurrencyPairs (context: HttpContext) =
    async {
        try
            let currencyPair = findCurrencyPairs
            return (Successful.OK "Success\n", $"Got cross-trade currency pairs: %A{currencyPair}")
        with
        | ex ->
            return (RequestErrors.BAD_REQUEST ex.Message, $"Failed to get currency pairs: %s{ex.Message}")
    }
    
let getAnnualReturn (context: HttpContext) =
    async {
        let! currTradingState = getTradingState ()
        match currTradingState.TradingParams, currTradingState.StartTradingTime with
        | Some tradingParams, startTimeOpt ->
            try
                let initialAmount = tradingParams.InitialInvestmentAmount
                let! pnlValue = getCurrentPnL
                let annualReturn = AnnualizedMetric initialAmount startTimeOpt pnlValue // Pass start time & pnl
                return (Successful.OK "Success\n", $"Got annualReturn: %A{annualReturn}")
            with
            | ex -> return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get annual return: %s{ex.Message}")

        |_ -> return (RequestErrors.BAD_REQUEST "Error", "Trading parameters not set")
    }

let app =
    choose [
        POST >=> path "/api/strategy" >=> handleRequest setTradingParameters
        GET >=> path "/api/strategy" >=> handleRequest getTradingParameters
        POST >=> path "/api/trading" >=> handleEmptyRequest toggleTrading
        GET >=> path "/api/historical-arbitrage" >=> handleRequest getHistoricalArbitrage
        GET >=> path "/api/cross-trade-pair" >=> handleRequest getCrossTradeCurrencyPairs
        GET >=> path "/api/annual-return" >=> handleRequest getAnnualReturn
    ]


startWebServer defaultConfig app




