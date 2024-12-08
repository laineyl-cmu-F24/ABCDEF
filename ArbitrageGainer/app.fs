module app
open System
open System.IO
open Microsoft.AspNetCore.Http.Features
open Suave
open Suave.Operators
open Suave.Filters
open MongoDB.Driver
open MongoDB.Bson

open Service.ApplicationService.Historical
open Service.ApplicationService.Metric
open Service.ApplicationService.CrossTradedCurrencyPair
open Service.ApplicationService.PnL
open Service.ApplicationService.TradingState
open Core.Model.Models
open Service.ApplicationService.Toggle
open Infrastructure.Client.EmailClient
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
    
let tradingParams: TradingParameters option = Some {
    NumOfCrypto = 5
    MinSpreadPrice = 0.05M
    MinTransactionProfit = 5.0M
    MaxTransactionValue = 2000.0M
    MaxTradeValue = 5000.0M
    InitialInvestmentAmount = 0.0M 
    Email = None
    PnLThreshold = None
}
    
let initialState = {
    TradingParams = tradingParams
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
        
let handlePnLEvent (event: PnLEvent) =
    match event with
    | ThresholdExceeded ->
        let res = toggleTrading
        printfn "Exceeding threshold. Trading Stopper"
        sendEmail "pkotchav@andrew.cmu.edu" "Arbitrage Gainer" "Threshold is exceeded."
    | TradingStopped ->
        printfn "Trading Already Stopped"

// Subscribe the event handler
onPnLEvent.Add(handlePnLEvent)

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
        let logger = createLogger
        logger "Historical Arbitrage Analysis - Started"
        
        // Define the file path for historicalData.txt
        let filePath = Path.Combine("/app", "historicalData.txt")
        
        match File.Exists filePath with
        | true ->
            try
                let result = calculateHistoricalArbitrage filePath
                let tradeRecords =
                    result
                    |> Seq.map (fun (pair, opportunityCount) ->
                        { Id = ObjectId.GenerateNewId(); Pair = pair; OpportunityCount = opportunityCount }
                    )
                    |> Seq.toList
                let history = SetTradeHistory tradeRecords
                return (Successful.OK "Success\n", $"Got historical arbitrage %A{result}")
            with
            | ex ->
                printfn $"Failed to get historical arbitrage: %s{ex.Message}"
                return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get historical arbitrage: %s{ex.Message}")
        | false ->
            printfn "historicalData.txt not found"
            return (RequestErrors.NOT_FOUND "Error\n", "historicalData.txt not found")
    }

    
let getCrossTradeCurrencyPairs (context: HttpContext) =
    async {
        let logger = createLogger
        try
            logger "Get Cross Currency Pair - Started"
            let currencyPair = findCurrencyPairs
            return (Successful.OK "Success\n", $"Got cross-trade currency pairs: %A{currencyPair}")
        with
        | ex ->
            logger "Get Cross Currency Pair - Failed to Start"
            return (RequestErrors.BAD_REQUEST ex.Message, $"Failed to get currency pairs: %s{ex.Message}")
    }
    
let getAnnualReturn (context: HttpContext) =
    async {
        let! currTradingState = getTradingState ()
        match currTradingState.TradingParams, currTradingState.StartTradingTime with
        | Some tradingParams, startTimeOpt ->
            try
                let initialAmount = tradingParams.InitialInvestmentAmount
                let! pnlValue = getCurrentPnL ()
                let annualReturn = AnnualizedMetric initialAmount startTimeOpt pnlValue // Pass start time & pnl
                return (Successful.OK "Success\n", $"Got annualReturn: %A{annualReturn}")
            with
            | ex -> return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get annual return: %s{ex.Message}")

        |_ -> return (RequestErrors.BAD_REQUEST "Error", "Trading parameters not set")
    }
    
let getCurrPnl (context: HttpContext) =
    async {
        try
            let! pnl = getCurrentPnL ()
            printfn $"Got pnl: %A{pnl}"
            return (Successful.OK "Success\n", $"Got pnl: %A{pnl}")
        with
        | ex ->
            printfn "Error retrieving pnl"
            return (RequestErrors.BAD_REQUEST "Error", "Error retrieving pnl")
        
    }
let tryParseDateTime (dateTimeStr: string) =
    match DateTime.TryParse(dateTimeStr) with
    | true, value -> Some value
    | false, _ -> None
    
let dateTimeToDecimal (dateTime: DateTime) =
    let epoch = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    let timeSpan = dateTime.ToUniversalTime() - epoch
    timeSpan.TotalDays
    
let getHistoricalPnl (startDateTimeStr, endDateTimeStr): WebPart =
    fun ctx ->
        async {
            match tryParseDateTime startDateTimeStr, tryParseDateTime endDateTimeStr with
            | Some startDateTime, Some endDateTime ->
                // Call the asynchronous function
                let! res = getHistoricalPnLWithIn startDateTime endDateTime

                // Prepare the response
                let msg = sprintf "Start: %s, End: %s, Result: %A" startDateTimeStr endDateTimeStr res
                return! Successful.OK msg ctx
            | _ ->
                return! RequestErrors.BAD_REQUEST "Invalid date format or date range." ctx
        }


    // Successful.OK (sprintf "Start: %s, End: %s" startDateTimeStr endDateTimeStr)

let handleRequestGet func =
    fun (context: HttpContext) ->
        async {
            let! webPart, _ = func context
            return! webPart context
        }

let app =
    choose [
        POST >=> path "/api/strategy" >=> handleRequest setTradingParameters
        GET >=> path "/api/strategy" >=> handleRequest getTradingParameters
        POST >=> path "/api/trading" >=> handleEmptyRequest toggleTrading
        GET >=> path "/api/historical-arbitrage" >=> handleRequest getHistoricalArbitrage
        GET >=> path "/api/cross-trade-pair" >=> handleRequest getCrossTradeCurrencyPairs
        GET >=> path "/api/annual-return" >=> handleRequest getAnnualReturn
        POST >=> path "/api/pnl" >=> handleRequest togglePnL
        GET >=> path "/api/current-pnl" >=> handleRequest getCurrPnl
        GET >=> pathScan "/api/historical-pnl/%s/%s" (fun start end_ -> getHistoricalPnl start end_)
    ]


startWebServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 8080  ] } app




