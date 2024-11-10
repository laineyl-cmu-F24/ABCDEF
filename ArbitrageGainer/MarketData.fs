module MarketData

open System
open global.Models
open global.Workflow
open global.Interfaces
open global.WebSocketClient

let getRealTimeData =
    let uri = Uri("wss://socket.polygon.io/crypto")
    let apiKey = "phN6Q_809zxfkeZesjta_phpgQCMB2Dw"
    
    //replace with actual user-defined trading Params
    let tradingParams = {
        NumOfCryptos = 3
        MinPriceSpread = 10.0M
        MinTransactionProfit = 5.0M
        MaxTradingValue = 10000.0M
        InitialInvestmentAmount = 50000.0M
        Email = "your_email@example.com"
    }
    
    // Replace with actual crossTradedCryptos
    let crossTradedCryptos = Set [
        CryptoSymbol "BTC-USD"
    ]
    
    // Create an instance of IWebSocketClient
    let webSocketClient = WebSocketClient(uri, apiKey) :> IWebSocketClient
    
    let result =
        runTradingWorkflow tradingParams crossTradedCryptos webSocketClient |> Async.RunSynchronously
    
    match result with
    | Ok () ->
        printfn "WebSocket client started successfully."
        0
    | Error errMsg ->
        match errMsg with
        | ConnectionError msg -> printfn "Connection error: %s" msg
        | AuthenticationError msg -> printfn "Authentication error: %s" msg
        | SubscriptionError msg -> printfn "Subscription error: %s" msg
        | DataError msg -> printfn "Data error: %s" msg
        1
    