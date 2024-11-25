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
open Service.ApplicationService.Workflow
open Service.ApplicationService.MarketData
open Service.ApplicationService.PnL
open Service.ApplicationService.TradingState
open Core.Model.Models
open Service.ApplicationService.Toggle

let handleRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func context
            return! response context
        }

// handle requests that does not involve stateAgent      
let handleSimpleRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func context
            return! response context
        }
        
let setTradingParameters (context: HttpContext) =
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
    
let getTradingParameters (context: HttpContext) =
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
                    return (Successful.OK "Success\n", $"Got historical arbitrage {result}")
                with
                | ex ->
                    return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get historical arbitrage: {ex.Message}")
            | _ -> return (RequestErrors.NOT_FOUND "Error\n", "File not found")
        | _ -> return (RequestErrors.BAD_REQUEST "Error\n", "No file path input")
    }
    
let getCrossTradeCurrencyPairs (context: HttpContext) =
    async {
        try
            let currencyPair = findCurrencyPairs
            return (Successful.OK "Success\n", $"Got cross-trade currency pairs: {currencyPair}")
        with
        | ex ->
            return (RequestErrors.BAD_REQUEST ex.Message, $"Failed to get currency pairs: {ex.Message}")
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
                return (Successful.OK "Success\n", $"Got annualReturn: {annualReturn}")
            with
            | ex -> return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get annual return: {ex.Message}")

        |_ -> return (RequestErrors.BAD_REQUEST "Error", "Trading parameters not set")
    }

let getPnL (context: HttpContext) =
    async {
        let! currentPnL = getCurrentPnL // Await the async result
        let responseFunc = Successful.OK (sprintf "Current P&L: %.2f" currentPnL)
        return (responseFunc, ())
    }

let getHistoricalPnL (context: HttpContext) =
    async {
        let query = context.request.query
        let findParam key =
            query
            |> List.tryFind (fun (k, _) -> k = key)
            |> Option.bind snd // Extract the value from the option tuple

        let startingStrOpt = findParam "starting"
        let endingStrOpt = findParam "ending"
        
        match startingStrOpt, endingStrOpt with
        | Some startingStr, Some endingStr ->
            match DateTime.TryParse(startingStr), DateTime.TryParse(endingStr) with
            | (true, starting), (true, ending) ->
                let! historicalPnL = getHistoricalPnLWithIn starting ending
                match historicalPnL with
                | Some pnl -> 
                    let responseFunc = Successful.OK (sprintf "Historical P&L: %.2f" pnl)
                    return (responseFunc, ()) // Return the tuple
                | None -> 
                    let responseFunc = RequestErrors.BAD_REQUEST "No historical P&L data found"
                    return (responseFunc, ())
            | _ -> 
                let responseFunc = RequestErrors.BAD_REQUEST "Invalid date format"
                return (responseFunc, ())
        | _ -> 
            let responseFunc = RequestErrors.BAD_REQUEST "Missing starting or ending parameter"
            return (responseFunc, ())
    }

let setPnlThreshold (context: HttpContext) =
    async {
        let req = context.request
        match req.formData "threshold" with
        | Choice1Of2 thresholdStr ->
            match Decimal.TryParse(thresholdStr) with
            | true, threshold when threshold >= 0m ->
                let! currentState = getTradingState ()
                match currentState.TradingParams with
                | Some tp ->
                    let newTp = { tp with PnLThreshold = Some threshold }
                    let! _ = stateAgent.PostAndAsyncReply(fun reply -> SetTradingParameters newTp)
                    let responseFunc = Successful.OK (sprintf "PnL Threshold set to %.2f\n" threshold)
                    return (responseFunc, ())
                | None ->
                    let responseFunc = RequestErrors.BAD_REQUEST "Trading parameters are not set\n"
                    return (responseFunc, ())
            | _ ->
                let responseFunc = RequestErrors.BAD_REQUEST "Invalid threshold value\n"
                return (responseFunc, ())
        | _ ->
            let responseFunc = RequestErrors.BAD_REQUEST "Missing required threshold parameter\n"
            return (responseFunc, ())
    }

let app =
    choose [
        POST >=> path "/api/strategy" >=> handleRequest setTradingParameters
        GET >=> path "/api/strategy" >=> handleRequest getTradingParameters
        POST >=> path "/api/trading" >=> handleRequest toggleTrading
        GET >=> path "/api/historical-arbitrage" >=> handleRequest getHistoricalArbitrage
        GET >=> path "/api/cross-trade-pair" >=> handleRequest getCrossTradeCurrencyPairs
        GET >=> path "/api/annual-return" >=> handleRequest getAnnualReturn
        GET >=> path "/api/pnl" >=> handleSimpleRequest getPnL
        GET >=> path "/api/pnl/" >=> handleSimpleRequest getHistoricalPnL 
        POST >=> path "/api/pnl" >=> handleRequest setPnlThreshold
    ]

startWebServer defaultConfig app
