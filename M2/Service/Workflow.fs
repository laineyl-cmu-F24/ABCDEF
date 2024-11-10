module M2.Service.Workflow

open M2.Service.Cache
open M2.Core.Models
open M2.Core.Interfaces
open M2.Core.ArbitrageAnalysis
open M2.Core.Helper
open M2.Core.ParsingMessage

let runTradingWorkflow tradingParams crossTradedSet (webSocketClient: IWebSocketClient): Async<Result<unit, DomainError>> =
    let cacheAgent = createCacheAgent ()
    
    // Step 1: Retrieve historical arbitrage data
    let historicalDataResult = retrieveHistoricalData ()
    
    // Step 2: Get top N aarbitrage opportunities cryptocurrencies 
    let topNCryptosResult = historicalDataResult |> map (getTopNCurrencies tradingParams.NumOfCryptos)
    
    // Step 3: Check with cross-traded cryptocurrencies
    let symbolsResult =
        topNCryptosResult
        |> map (filterCrossTraded crossTradedSet)
        |> map (List.map (fun (CryptoSymbol symbol) -> symbol))
    
    // Step 4: Use the webSocketClient to send messages and receive data
    let connectAndReceive symbols =
        async{
            let! connectResult = webSocketClient.Connect()
            match connectResult with
                | Error e -> return Error e
                | Ok () ->
                    printfn "Connected and authenticated."
                    
                    let subscriptionParameters = 
                        symbols 
                        |> List.map (fun s -> $"XQ.{s}") 
                        |> String.concat ","
                    
                    let subscriptionMessage = { action = "subscribe"; params = subscriptionParameters }
                    let! subResult = webSocketClient.SendMessage subscriptionMessage
                    match subResult with
                        | Error errMsg -> return Error errMsg
                        | Ok () ->
                            printfn "Subscription successful."
                            
                            let rec receiveLoop() = async {
                                let! receiveResult = webSocketClient.ReceiveMessage()
                                match receiveResult with
                                |Error e -> return Error e
                                |Ok message ->
                                    match parseMessage message with
                                    | QuoteReceived quote ->
                                        cacheAgent.Post(UpdateCache quote)
                                        
                                        printfn "Printing cache after update:"
                                        cacheAgent.Post(PrintCache)
                                        
                                        return! receiveLoop()
                                    | StatusReceived statusMsg ->
                                        return! receiveLoop()
                                    |ParseError err -> return! receiveLoop()
                            }
                            let! receiveResult = receiveLoop()
                            return receiveResult        
        }
    
    async {
        match symbolsResult with
        | Error e -> return Error e
        | Ok symbols ->
            let! connectResult = connectAndReceive symbols
            return connectResult
    }
        
        
    
    
    

