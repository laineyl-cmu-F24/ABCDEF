module Service.ApplicationService.Toggle
open System
open System.IO
open Suave
open MongoDB.Driver
open Core.Model.Models
open Infrastructure.Client.WebSocketClient
open Service.ApplicationService.CrossTradedCurrencyPair
open Service.ApplicationService.Workflow
open Service.ApplicationService.MarketData
open  Service.ApplicationService.TradingAgent
open Service.ApplicationService.TradingState
open Service.ApplicationService.Cache


let toggleTrading () =
    async {
        let! currTradingState = getTradingState ()
        match currTradingState.IsTradingActive with
        | false ->
            match currTradingState.TradingParams with
            |Some tradingParams ->
                let tradingParams = tradingParams
                let tradeHistory = currTradingState.TradeHistory
                //need to be change with actual
                let crossTradedCryptos = Set.ofSeq findCurrencyPairs
                // let crossTradedCryptos = Set.ofSeq ["MKR-USD"; "USD-BTC"; "SOL-USD"; "DOT-USD"]
                //let uri = Uri("wss://socket.polygon.io/crypto")
                //let apiKey = "phN6Q_809zxfkeZesjta_phpgQCMB2Dw"
                let uri = Uri("wss://one8656-live-data.onrender.com/")
                let apiKey = ""
                let filteredCrypto = runTradingWorkflow tradingParams crossTradedCryptos tradeHistory
                let closeFunc, clientAsync = WebSocketClient uri apiKey filteredCrypto tradingParams
                let! result = toggleRealTimeData true tradingParams crossTradedCryptos tradeHistory uri apiKey
                match result with
                | Ok closeFunc ->
                    // let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(true, Some closeFunc, reply))
                    //  replaced the above with refactored stateAgent:
                    setIsTradingActive true
                    setWebSocketClientCloseFunc (Some closeFunc)
                    printfn "Trading started."
                    let! trade = processArbitrageOpportunities (createCacheAgent()) tradingParams
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
                    // let! updatedState = stateAgent.PostAndAsyncReply(fun reply -> ToggleTrading(false, None, reply))
                    //  replaced the above with refactored stateAgent:
                    setIsTradingActive false
                    setWebSocketClientCloseFunc None
                    printfn "Trading stopped."
                    return (Successful.OK "Trading stopped\n", ())
                | Error e ->
                    printfn "Error during close: %A" e
                    return (RequestErrors.BAD_REQUEST "Failed to stop trading", ())
            | None ->
                return (RequestErrors.BAD_REQUEST "No active trading session to stop", ())
    }