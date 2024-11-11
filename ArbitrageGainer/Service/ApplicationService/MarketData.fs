module Service.ApplicationService.MarketData

open System
open Core.Model.Models
open Service.ApplicationService.Workflow
open Core.Model.Interfaces
open Infrastructure.Client.WebSocketClient
    
let toggleRealTimeData flag numOfCrypto (crossTradedCryptos: Set<string>) tradeHistory (webSocketClient:IWebSocketClient)=
    async {
        match flag with
        | false ->
            let! closeResult = webSocketClient.Close()
            match closeResult with
            | Ok () ->
                printfn "WebSocket client closed successfully."
                return Ok ()
            | Error e ->
                printfn "Error during close"
                return Error e
        | true ->
            let! result =
                runTradingWorkflow numOfCrypto crossTradedCryptos webSocketClient tradeHistory
            match result with
            | Ok () ->
                printfn "WebSocket client started successfully."
                return Ok()
            | Error errMsg ->
                return Error errMsg
    }