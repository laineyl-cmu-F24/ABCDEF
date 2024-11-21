module Service.ApplicationService.MarketData

open System
open Core.Model.Models
open Service.ApplicationService.Workflow
open Infrastructure.Client.WebSocketClient
    
let toggleRealTimeData flag numOfCrypto (crossTradedCryptos: Set<string>) tradeHistory uri apiKey : Async<DomainResult<unit -> Async<DomainResult<unit>>>> =
    async {
        let filteredCryptos = runTradingWorkflow numOfCrypto crossTradedCryptos tradeHistory
        let closeFunc, clientAsync = WebSocketClient uri apiKey filteredCryptos
        match flag with
        | false ->
            return Error (ConnectionError "toggleRealTimeData called with flag false without closeFunc")
        | true ->
            let closeFunc, clientAsync = WebSocketClient uri apiKey filteredCryptos
            Async.Start(clientAsync)
            return Ok closeFunc
    }