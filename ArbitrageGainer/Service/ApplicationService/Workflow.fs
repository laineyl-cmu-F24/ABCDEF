module Service.ApplicationService.Workflow

open Service.ApplicationService.Cache
open Core.Model.Models
open Core.Model.Interfaces
open Core.CoreService.Helper
open Core.CoreService.ParsingMessage

let runTradingWorkflow numOfCrypto (crossTradedCryptos: Set<string>) (webSocketClient: IWebSocketClient) (tradeHistory: TradeRecord list)=
    let cacheAgent = createCacheAgent ()

    // Step 1: Get top N aarbitrage opportunities cryptocurrencies 
    let topNCryptosResult =
        tradeHistory
        |> List.sortByDescending (fun record -> record.OpportunityCount)
        |> List.truncate numOfCrypto
        |> List.map (fun record -> record.Pair)
    
    if List.length tradeHistory < numOfCrypto then
        printfn "Warning: Requested top %d cryptos, but only %d available in trade history." numOfCrypto (List.length tradeHistory)
    
    // Step 2: Check with cross-traded cryptocurrencies
    let filteredCryptos =
        topNCryptosResult
        |> List.filter (fun pair -> crossTradedCryptos.Contains pair)
    
    // Step 3: Use the webSocketClient to send messages and receive data
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
                                match webSocketClient.IsOpen with
                                | false ->
                                    printfn "WebSocket is closed, stopping receive loop."
                                    return Ok ()
                                | true ->
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
        let! connectResult = connectAndReceive filteredCryptos
        return connectResult
    }
        
        
    
    
    

