module Service.ApplicationService.TradingState

// Assuming these types are defined in your project
open Core.Model.Models  // For TradingParameters, TradeRecord
open Infrastructure.Client.WebSocketClient  // For DomainResult

/// Represents the overall system state
type SystemState = {
    TradingParams: TradingParameters option
    IsTradingActive: bool
    TradeHistory: TradeRecord list
    WebSocketClientCloseFunc: Option<unit -> Async<DomainResult<unit>>>
    StartTradingTime: int64 option
}

/// Messages for the state agent to process
type AgentMessage =
    | GetTradingState of AsyncReplyChannel<SystemState>
    | SetTradingParameters of TradingParameters
    | GetTradingParameters of AsyncReplyChannel<TradingParameters option>
    | SetIsTradingActive of bool
    | GetIsTradingActive of AsyncReplyChannel<bool>
    | SetTradeHistory of TradeRecord list
    | GetTradeHistory of AsyncReplyChannel<TradeRecord list>
    | SetWebSocketClientCloseFunc of Option<unit -> Async<DomainResult<unit>>>
    | GetWebSocketClientCloseFunc of AsyncReplyChannel<Option<unit -> Async<DomainResult<unit>>>>
    | SetStartTradingTime of int64 option
    | GetStartTradingTime of AsyncReplyChannel<int64 option>

/// The initial state of the system

let tradingParams: TradingParameters option = Some {
    NumOfCrypto = 5
    MinSpreadPrice = 0.05M
    MinTransactionProfit = 5.0M
    MaxTransactionValue = 2000.0M
    MaxTradeValue = 5000.0M
    InitialInvestmentAmount = 5000.0M 
    Email = Some "pkotchav@andrew.cmu.edu"
    PnLThreshold = None
}

let initialState = {
    TradingParams = tradingParams
    IsTradingActive = false
    TradeHistory = []
    WebSocketClientCloseFunc = None
    StartTradingTime = None
}

/// The state agent that manages the SystemState
let stateAgent = MailboxProcessor<AgentMessage>.Start(fun inbox ->
    let rec loop (state: SystemState) =
        async {
            let! msg = inbox.Receive()
            match msg with
            | GetTradingState reply ->
                reply.Reply(state)
                return! loop state
            | SetTradingParameters tp ->
                let newState = { state with TradingParams = Some tp }
                return! loop newState
            | GetTradingParameters reply ->
                reply.Reply state.TradingParams
                return! loop state
            | SetIsTradingActive isActive ->
                let newState = { state with IsTradingActive = isActive }
                return! loop newState
            | GetIsTradingActive reply ->
                reply.Reply state.IsTradingActive
                return! loop state
            | SetTradeHistory tradeHistory ->
                let newState = { state with TradeHistory = tradeHistory }
                return! loop newState
            | GetTradeHistory reply ->
                reply.Reply state.TradeHistory
                return! loop state
            | SetWebSocketClientCloseFunc closeFunc ->
                let newState = { state with WebSocketClientCloseFunc = closeFunc }
                return! loop newState
            | GetWebSocketClientCloseFunc reply ->
                reply.Reply state.WebSocketClientCloseFunc
                return! loop state
            | SetStartTradingTime startTime ->
                let newState = { state with StartTradingTime = startTime }
                return! loop newState
            | GetStartTradingTime reply ->
                reply.Reply state.StartTradingTime
                return! loop state
        }
    loop initialState
)

/// get the current SystemState
let getTradingState () =
    stateAgent.PostAndAsyncReply(fun reply -> GetTradingState reply)
    
/// Sets the trading parameters
let setTradingParameters (tp: TradingParameters) =
    stateAgent.Post(SetTradingParameters tp)

/// Gets the current trading parameters
let getTradingParameters () =
    stateAgent.PostAndAsyncReply(fun reply -> GetTradingParameters reply)

/// Sets the trading active status
let setIsTradingActive (isActive: bool) =
    stateAgent.Post(SetIsTradingActive isActive)

/// Gets the current trading active status
let getIsTradingActive () =
    stateAgent.PostAndAsyncReply(fun reply -> GetIsTradingActive reply)

/// Sets the trade history
let setTradeHistory (tradeHistory: TradeRecord list) =
    stateAgent.Post(SetTradeHistory tradeHistory)

/// Gets the current trade history
let getTradeHistory () =
    stateAgent.PostAndAsyncReply(fun reply -> GetTradeHistory reply)

/// Sets the WebSocket client close function
let setWebSocketClientCloseFunc (closeFunc: Option<unit -> Async<DomainResult<unit>>>) =
    stateAgent.Post(SetWebSocketClientCloseFunc closeFunc)

/// Gets the WebSocket client close function
let getWebSocketClientCloseFunc () =
    stateAgent.PostAndAsyncReply(fun reply -> GetWebSocketClientCloseFunc reply)

/// Sets the start trading time
let setStartTradingTime (startTime: int64 option) =
    stateAgent.Post(SetStartTradingTime startTime)

/// Gets the start trading time
let getStartTradingTime () =
    stateAgent.PostAndAsyncReply(fun reply -> GetStartTradingTime reply)
