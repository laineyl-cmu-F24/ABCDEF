module Infrastructure.Client.WebSocketClient

open System

open System.Net.WebSockets
open System.Text.Json
open System.Threading
open System.Text
open Core.Model.Models
open Core.CoreService.ParsingMessage
open Service.ApplicationService.Cache
open Service.ApplicationService.TradingAgent

let WebSocketClient uri apiKey symbols tradingParams =
    let wsClient = new ClientWebSocket()
    let cacheAgent = createCacheAgent()
    
    // Define a function to send a message to the WebSocket
    let sendJsonMessage message =
            async {
                try
                    let messageJson = JsonSerializer.Serialize(message)
                    let messageBytes = Encoding.UTF8.GetBytes(messageJson)
                    do! wsClient.SendAsync(ArraySegment(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask
                    return Ok ()
                with ex ->
                    return Error (DataError ex.Message)
            }
            
    let connect () = async {
        try
            do! wsClient.ConnectAsync(uri, CancellationToken.None) |> Async.AwaitTask
            let authMessage = { action = "auth"; params = apiKey }
            do! sendJsonMessage authMessage |> Async.Ignore
            return Ok()
        
        with ex -> return Error (ConnectionError ex.Message)
    }
            
    let receiveMessage () =
        async {
            let buffer = Array.zeroCreate 10024
            let segment = new ArraySegment<byte>(buffer)
            try
                let! result =
                    wsClient.ReceiveAsync(segment, CancellationToken.None)
                    //Convert a .NET task into an async workflow
                    //Asynchronously await the completion of an asynchronous computation (non-blocking)
                    |> Async.AwaitTask

                match result.MessageType with
                | WebSocketMessageType.Text ->
                    let message = Encoding.UTF8.GetString(buffer, 0, result.Count)
                    //printfn "Real Time Market Data Message: %s" message
                    return Ok message
                | WebSocketMessageType.Close ->
                    // Handle WebSocket closure
                    return Error (ConnectionError "WebSocket closed by the server.")
                | _ -> return Error (DataError "Unexpected message type.")
            with ex ->
                return Error (DataError ex.Message)
        }
     
    let close ()=
        async {
        try
            match wsClient.State with
            | WebSocketState.Open
            | WebSocketState.CloseReceived
            | WebSocketState.CloseSent ->
                do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask
                printfn "WebSocket client closed."
                return Ok ()
            | _ ->
                printfn "WebSocket is not connected. No need to close."
                return Ok ()
        with ex ->
            printfn "Exception during WebSocket close: %A" ex
            return Error (ConnectionError ex.Message)
    }
    
    let IsOpen() = wsClient.State = WebSocketState.Open
    
    let connectAndReceive() =
        async{
            let! connectResult = connect()
            match connectResult with
                | Error e -> return Error e
                | Ok () ->
                    printfn "Connected and authenticated."
                    
                    let subscriptionParameters = 
                        symbols 
                        |> List.map (fun s -> $"XQ.{s}") 
                        |> String.concat ","
                    
                    let subscriptionMessage = { action = "subscribe"; params = subscriptionParameters }
                    let! subResult = sendJsonMessage subscriptionMessage
                    match subResult with
                        | Error errMsg -> return Error errMsg
                        | Ok () ->
                            printfn "Subscription successful."
                            
                            let rec receiveLoop() = async {
                                match IsOpen() with
                                | false ->
                                    printfn "WebSocket is closed, stopping receive loop."
                                    return Ok ()
                                | true ->
                                    let! receiveResult = receiveMessage()
                                    match receiveResult with
                                    |Error e -> return Error e
                                    |Ok message ->
                                        match parseMessage message with
                                        | Ok (QuoteReceived quote) ->
                                            do! processArbitrageOpportunities cacheAgent tradingParams
                                            cacheAgent.Post(UpdateCache quote)
                                            return! receiveLoop()
                                        | Ok (StatusReceived statusMsg) ->
                                            return! receiveLoop()
                                        | Error (ParseError err) ->
                                            return! receiveLoop()
                                }
                            let! receiveResult = receiveLoop()
                            return receiveResult        
        }
        
    let clientAsync = async {
        let! result = connectAndReceive()
        match result with
        | Ok () -> ()
        | Error e -> printfn "WebSocket client error: %A" e
    }
    
    (close, clientAsync)


           