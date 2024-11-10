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

type AgentMessage =
    | SetTradingParameters of TradingParameters * AsyncReplyChannel<SystemState>
    | GetCurrentState of AsyncReplyChannel<SystemState>
    | ToggleTrading of bool * AsyncReplyChannel<SystemState>
    
    
let initialState = {
    TradingParams = None
    IsTradingActive = false
    TradeHistory = []
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
            | ToggleTrading (isActive, reply) ->
                let updatedState = { state with IsTradingActive = isActive }
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
            return (Successful.OK "Trading parameters updated successfully", updatedState)
        | _ -> return (RequestErrors.BAD_REQUEST "Invalid parameters provided", initialState)
    }
    
let getTradingParameters (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let! currentState = stateAgent.PostAndAsyncReply(GetCurrentState) 
        match currentState.TradingParams with
        | Some tradingParams ->
            let json = System.Text.Json.JsonSerializer.Serialize(tradingParams)
            return (Successful.OK json, ())
        | None ->
            return (RequestErrors.NOT_FOUND "Error when getting trading parameters", ())
    }
    
let toggleTrading (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let! currTradingState = stateAgent.PostAndAsyncReply(GetCurrentState)
        match currTradingState.IsTradingActive with
        | false ->
            let! updateState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(true, reply))
            
            let result = toggleRealTimeData true
            return (Successful.OK "Trading started", ())
        | true ->
            let! updateState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(false, reply))
            
            let result = toggleRealTimeData false
            return (Successful.OK "Trading stopped", ())
    }
    
let getHistoricalArbitrage (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let req = context.request
        match req.formData "file" with
        | Choice1Of2 filePath ->
            // printfn $"Requested file path: %s{filePath}"
            match filePath with
            | filePath when File.Exists filePath -> 
                let result = calculateHistoricalArbitrage filePath
                return (Successful.OK "Successfully got historical arbitrage", ())
            | _ -> return (RequestErrors.NOT_FOUND "File not found", ())
        | _ -> return (RequestErrors.BAD_REQUEST "Error during calculation", ())
    }
    
let getCrossTradeCurrencyPairs (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let currencyPair = findPairs // TODO: get currency pair
        match currencyPair with
        | currencyPair ->
            return (Successful.OK "Successfully got cross-trade currency pairs", ())
        | _ -> return (RequestErrors.BAD_REQUEST "Error during retrieval", ())
    }
    
let getAnnualReturn (stateAgent: MailboxProcessor<AgentMessage>) (context: HttpContext) =
    async {
        let! currTradingState = stateAgent.PostAndAsyncReply(GetCurrentState)
        match currTradingState.TradingParams with
        | Some tradingParams ->
            let initialAmount = tradingParams.InitialInvestmentAmount
            let annualReturn = AnnualizedMetric initialAmount // Perform the annual return calculation here

            match annualReturn with
            | annualReturn ->
                return (Successful.OK "Successfully got annual return", ())
            | _ -> return (RequestErrors.BAD_REQUEST "Error during calculation", ())

        | None -> return (RequestErrors.BAD_REQUEST "Trading parameters are not set", ())
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
