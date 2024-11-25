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
open Infrastructure.Client.WebSocketClient
open  Service.ApplicationService.TradingAgent
open Service.ApplicationService.Cache
open Service.ApplicationService.Toggle


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

let stateAgent = MailboxProcessor<AgentMessage>.Start(fun inbox ->
    let rec loop state =
        async {
            let! message = inbox.Receive()
            match message with
            | SetTradingParameters (p, reply) ->
                // printfn $"Current State: %A{state}"
                let updatedState = { state with TradingParams = Some p }
                reply.Reply(updatedState)
                return! loop updatedState
            | GetCurrentState reply ->
                reply.Reply(state)
                return! loop state
            | GetTradeHistory (newTrades, reply) ->
                let updatedState = { state with TradeHistory = newTrades }
                reply.Reply(updatedState)
                return! loop updatedState
            | ToggleTrading (isActive, closeFuncOpt, reply) ->
                let updatedState =
                    match isActive with
                    | true -> {
                            state with
                                IsTradingActive = isActive
                                WebSocketClientCloseFunc =  closeFuncOpt
                                StartTradingTime = Some (DateTimeOffset.Now.ToUnixTimeMilliseconds())
                                }
                    | false -> {
                            state with
                                 IsTradingActive = false
                                 WebSocketClientCloseFunc = None
                                 StartTradingTime = None // Clear start time
                                 }
                reply.Reply(updatedState)
                return! loop updatedState
        }
    loop initialState
)

let handleRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func context
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
    

let toggleTrading (context: HttpContext) =
    async {
        let! currState = getTradingState ()
        match currState.IsTradingActive with
        | false ->
            match currState.TradingParams with
            | Some tradingParams ->
                let tradeHistory = currState.TradeHistory
                let crossTradedCryptos = Set.ofSeq findCurrencyPairs
                let uri = Uri("wss://one8656-live-data.onrender.com/")
                let apiKey = ""
                let filteredCrypto = runTradingWorkflow tradingParams crossTradedCryptos tradeHistory
                let closeFunc, clientAsync = WebSocketClient uri apiKey filteredCrypto tradingParams
                let! result = toggleRealTimeData true tradingParams crossTradedCryptos tradeHistory uri apiKey
                match result with
                | Ok closeFunc ->
                    let! updatedState =
                        stateAgent.PostAndAsyncReply(fun reply ->
                            ToggleTrading(true, Some closeFunc, reply)
                        )
                    printfn "Trading started."
                    let! _ = processArbitrageOpportunities (createCacheAgent()) tradingParams
                    return (Successful.OK "Trading started\n", ())
                | Error e ->
                    printfn "Failed to start trading: %A" e
                    return (RequestErrors.BAD_REQUEST (sprintf "Failed to start trading: %A" e), ())
            | None ->
                return (RequestErrors.BAD_REQUEST "Trading parameters are not set", ())
        | true ->
            match currState.WebSocketClientCloseFunc with
            | Some closeFunc ->
                let! closeResult = closeFunc()
                match closeResult with
                | Ok () ->
                    let! updatedState =
                        stateAgent.PostAndAsyncReply(fun reply ->
                            ToggleTrading(false, None, reply)
                        )
                    printfn "Trading stopped."
                    return (Successful.OK "Trading stopped\n", ())
                | Error e ->
                    printfn "Error during close: %A" e
                    return (RequestErrors.BAD_REQUEST "Failed to stop trading", ())
            | None ->
                return (RequestErrors.BAD_REQUEST "No active trading session to stop", ())
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
                    return (Successful.OK "Success\n", $"Got historical arbitrage %A{result}")
                with
                | ex ->
                    return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get historical arbitrage: %s{ex.Message}")
            | _ -> return (RequestErrors.NOT_FOUND "Error\n", "File not found")
        | _ -> return (RequestErrors.BAD_REQUEST "Error\n", "No file path input")
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
        POST >=> path "/api/trading" >=> handleRequest toggleTrading
        GET >=> path "/api/historical-arbitrage" >=> handleRequest getHistoricalArbitrage
        GET >=> path "/api/cross-trade-pair" >=> handleRequest getCrossTradeCurrencyPairs
        GET >=> path "/api/annual-return" >=> handleRequest getAnnualReturn
    ]

startWebServer defaultConfig app
