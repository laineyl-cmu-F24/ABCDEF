module api
open System
open System.IO
open Suave
open Suave.Operators
open Suave.Filters

open Historical
open Metric
open CrossTradedCurrencyPair
open MarketData
open Core.Models
open Core.Interfaces
open Infrastructure.WebSocketClient

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

type SystemState = {
    TradingParams: TradingParameters option
    IsTradingActive: bool
    TradeHistory: TradeRecord list
    WebSocketClient: IWebSocketClient option
}

type AgentMessage =
    | SetTradingParameters of TradingParameters * AsyncReplyChannel<SystemState>
    | GetCurrentState of AsyncReplyChannel<SystemState>
    | GetTradeHistory of TradeRecord list * AsyncReplyChannel<SystemState>
    | ToggleTrading of bool * IWebSocketClient option * AsyncReplyChannel<SystemState>
    
    
let initialState = {
    TradingParams = None
    IsTradingActive = false
    TradeHistory = []
    WebSocketClient = None
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
            | ToggleTrading (isActive, wsClientOpt, reply) ->
                let updatedState = { state with IsTradingActive = isActive; WebSocketClient = wsClientOpt; }
                reply.Reply(updatedState)
                return! loop updatedState
        }
    loop initialState
)

let handleRequest func =
    fun (context: HttpContext) ->
        async {
            let! response, _ = func stateAgent context
            return! response context
        }

let setTradingParameters (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
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
                MinTransactionProfit = decimal minTransactProfit
                MaxTransactionValue = decimal maxTransactVal
                MaxTradeValue = decimal maxTradeVal 
                InitialInvestmentAmount = decimal initialInvestment
                Email = if String.IsNullOrWhiteSpace email then None else Some email
                PnLThreshold = if String.IsNullOrWhiteSpace pnlThreshold then None else Some (decimal pnlThreshold)
            }
            let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> SetTradingParameters(parameters, reply))
            // printfn $"Updated State: %A{updatedState}"
            return (Successful.OK "Trading parameters updated successfully\n", updatedState)
        | _ -> return (RequestErrors.BAD_REQUEST "Invalid parameters provided\n", initialState)
    }
    
let getTradingParameters (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let! currentState = stateAgent.PostAndAsyncReply(GetCurrentState) 
        match currentState.TradingParams with
        | Some tradingParams ->
            let json = System.Text.Json.JsonSerializer.Serialize(tradingParams)
            return (Successful.OK json, ())
        | None ->
            return (RequestErrors.NOT_FOUND "Error when getting trading parameters\n", ())
    }
    

let toggleTrading (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext)  =
    async {
        let! currTradingState = stateAgent.PostAndAsyncReply(GetCurrentState)
        match currTradingState.IsTradingActive with
        | false ->
            match currTradingState.TradingParams with
            |Some tradingParams ->
                let numOfCrypto = tradingParams.NumOfCrypto
                let tradeHistory = currTradingState.TradeHistory
                //need to be change with actual
                let crossTradedCryptos = Set.ofSeq findPairs
                let uri = Uri("wss://socket.polygon.io/crypto")
                let apiKey = "phN6Q_809zxfkeZesjta_phpgQCMB2Dw"
                let webSocketClient = WebSocketClient(uri, apiKey) :> IWebSocketClient
                Async.Start(async {
                    let! result = toggleRealTimeData true numOfCrypto crossTradedCryptos tradeHistory webSocketClient
                    match result with
                    | Ok () ->
                        printfn "Trading workflow completed successfully."
                    | Error e ->
                        printfn "Trading workflow failed with error: %A" e
                })
                let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(true, Some webSocketClient, reply))
                return (Successful.OK "Trading started\n", ())
            | None ->
                return (RequestErrors.BAD_REQUEST "Trading parameters are not set", ())
        | true ->
            match currTradingState.WebSocketClient with
            | Some webSocketClient ->
                let! closeResult = webSocketClient.Close()
                match closeResult with
                | Ok () ->
                    let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(false, None, reply))
                    return (Successful.OK "Trading stopped\n", ())
                | Error e ->
                    let! _ = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(true, Some webSocketClient, reply))
                    return (RequestErrors.BAD_REQUEST "Failed to stop trading", ())
            |None -> return (RequestErrors.BAD_REQUEST "No active trading session to stop", ())
    }
    
let getHistoricalArbitrage (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
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
                    let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> GetTradeHistory(tradeRecords, reply)) 
                    return (Successful.OK "Success\n", $"Got historical arbitrage %A{result}")
                with
                | ex ->
                    return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get historical arbitrage: %s{ex.Message}")
            | _ -> return (RequestErrors.NOT_FOUND "Error\n", "File not found")
        | _ -> return (RequestErrors.BAD_REQUEST "Error\n", "No file path input")
    }
    
let getCrossTradeCurrencyPairs (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        try
            let currencyPair = findCurrencyPairs
            return (Successful.OK "Success\n", $"Got cross-trade currency pairs: %A{currencyPair}")
        with
        | ex ->
            return (RequestErrors.BAD_REQUEST ex.Message, $"Failed to get currency pairs: %s{ex.Message}")
    }
    
let getAnnualReturn (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let! currTradingState = stateAgent.PostAndAsyncReply(GetCurrentState)
        match currTradingState.TradingParams with
        | Some tradingParams ->
            try
                let initialAmount = tradingParams.InitialInvestmentAmount
                let annualReturn = AnnualizedMetric initialAmount // Perform the annual return calculation here

                return (Successful.OK "Success\n", $"Got annualReturn: %A{annualReturn}")
            with
                | ex ->
                    return (RequestErrors.BAD_REQUEST "Error\n", $"Failed to get annual return: %s{ex.Message}")

        | None -> return (RequestErrors.BAD_REQUEST "Error", "Trading parameters not set")
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
