module api
open System
open Suave
open Suave.Operators
open Suave.Filters

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
    | GetTradingParameters of AsyncReplyChannel<SystemState>
    | AddTradeRecord of TradeRecord * AsyncReplyChannel<SystemState>

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
            | GetTradingParameters reply ->
                reply.Reply(state)
                return! loop state
            | AddTradeRecord (trade, reply) ->
                let updatedState = { state with TradeHistory = trade :: state.TradeHistory }
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
        let! currentState = stateAgent.PostAndAsyncReply(fun reply -> GetTradingParameters reply)
        match currentState.TradingParams with
        | Some tradingParams ->
            let json = System.Text.Json.JsonSerializer.Serialize(tradingParams)
            return (Successful.OK json, ())
        | None ->
            return (RequestErrors.NOT_FOUND "Error when getting trading parameters", ())
    }

let app =
    choose [
        POST >=> path "/api/strategy" >=> handleRequest setTradingParameters
        GET >=> path "/api/strategy" >=> handleRequest getTradingParameters
    ]

startWebServer defaultConfig app
