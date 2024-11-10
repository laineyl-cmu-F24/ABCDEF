module Infrastructure.WebSocketClient

open System

open System.Net.WebSockets
open System.Text.Json
open System.Threading
open System.Text
open Core.Models
open Core.Interfaces

type WebSocketClient(uri, apiKey) =
    let wsClient = new ClientWebSocket()
    
    // Define a function to send a message to the WebSocket
    let sendJsonMessage message : Async<Result<unit, DomainError>> =
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
            
    let receiveMessage () : Async<Result<string, DomainError>> =
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
                    return Ok message
                | WebSocketMessageType.Close ->
                    // Handle WebSocket closure
                    return Error (ConnectionError "WebSocket closed by the server.")
                | _ -> return Error (DataError "Unexpected message type.")
            with ex ->
                return Error (DataError ex.Message)
        }
     
    let close () : Async<Result<unit, DomainError>> =
        async {
            try
                do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask
                return Ok ()
            with ex ->
                return Error (ConnectionError ex.Message)
        }

    interface IWebSocketClient with
        member _.Connect () = connect ()
        member _.SendMessage (message: 'T) = sendJsonMessage message
        member _.ReceiveMessage () = receiveMessage ()
        member _.Close () = close ()

           