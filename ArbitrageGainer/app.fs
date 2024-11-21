module app
open System
open System.IO
open Suave
open Suave.Operators
open Suave.Filters

open Service.ApplicationService.Historical
open Service.ApplicationService.Metric
open Service.ApplicationService.CrossTradedCurrencyPair
open Service.ApplicationService.Workflow
open Service.ApplicationService.MarketData
open Core.Model.Models
open Infrastructure.Client.WebSocketClient

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
    WebSocketClientCloseFunc: Option<unit -> Async<DomainResult<unit>>>
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
                let updatedState = { state with IsTradingActive = isActive; WebSocketClientCloseFunc =  closeFuncOpt; }
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
                let crossTradedCryptos = Set.ofSeq findCurrencyPairs
                //let crossTradedCryptos = Set.ofSeq ["MKR-USD"; "USD-BIT"]
                let uri = Uri("wss://socket.polygon.io/crypto")
                let apiKey = "phN6Q_809zxfkeZesjta_phpgQCMB2Dw"
                let filteredCrypto = runTradingWorkflow numOfCrypto crossTradedCryptos tradeHistory
                let closeFunc, clientAsync = WebSocketClient uri apiKey filteredCrypto
                let! result = toggleRealTimeData true numOfCrypto crossTradedCryptos tradeHistory uri apiKey
                match result with
                | Ok closeFunc ->
                    let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(true, Some closeFunc, reply))
                    printfn "Trading started."
                    return (Successful.OK "Trading started\n", ())
                | Error e ->
                    printfn "Failed to start trading: %A" e
                    return (RequestErrors.BAD_REQUEST (sprintf "Failed to start trading: %A" e), ())
            | None ->
                return (RequestErrors.BAD_REQUEST "Trading parameters are not set", ())
        | true ->
            match currTradingState.WebSocketClientCloseFunc with
            | Some closeFunc ->
                let! closeResult = closeFunc()
                match closeResult with
                | Ok () ->
                    let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(false, None, reply))
                    printfn "Trading stopped."
                    return (Successful.OK "Trading stopped\n", ())
                | Error e ->
                    printfn "Error during close: %A" e
                    return (RequestErrors.BAD_REQUEST "Failed to stop trading", ())
            | None ->
                return (RequestErrors.BAD_REQUEST "No active trading session to stop", ())
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
