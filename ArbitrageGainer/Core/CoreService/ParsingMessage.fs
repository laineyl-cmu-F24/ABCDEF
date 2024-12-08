module Core.CoreService.ParsingMessage

open System
open System.Text.Json
open Core.Model.Models

let toExchange (exchangeId: int) =
    match exchangeId with
    | 2 -> Bitfinex
    | 23 -> Kraken
    | 6 -> Bitstamp
    | _ -> failwith $"Unknown exchange: {exchangeId}"

let parseMessage(json: string) =
    try
        let messages = JsonSerializer.Deserialize<JsonElement[]>(json)
        match messages.Length > 0 with
        |true ->
            let message = messages.[0]
            let ev = message.GetProperty("ev").GetString()
            match ev with
            | "XQ" ->
                let exchangeId = message.GetProperty("x").GetInt32()
                match exchangeId with
                | 2 | 6 | 23 -> //Bitfinex, Bitstamp, and Kraken
                    let pair = message.GetProperty("pair").GetString()
                    let bidPrice = message.GetProperty("bp").GetDecimal()
                    let askPrice = message.GetProperty("ap").GetDecimal()
                    let askSize = message.GetProperty("as").GetDecimal()
                    let bidSize = message.GetProperty("bs").GetDecimal()
                    let timestamp = message.GetProperty("t").GetInt64()
                    let quote = {
                        Symbol = pair
                        Pair =  pair
                        Exchange = toExchange (exchangeId)
                        BidPrice = bidPrice
                        BidSize = bidSize 
                        AskPrice = askPrice
                        AskSize = askSize 
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime
                    }
                    Ok (QuoteReceived quote)
                |_ ->
                    Error (ParseError "Skipping quote from unsupported exchange")
            |"status" ->
                let status = message.GetProperty("status").GetString()
                let statusMessage = message.GetProperty("message").GetString()
                let statusMsg = { ev = "status"; status = status; message = statusMessage }
                Ok (StatusReceived statusMsg)
            |_ ->
                Error (ParseError $"Unknown or unsupported event type: {ev}")
        |false ->
            Error (ParseError "Empty message array")
    with ex ->
        Error (ParseError ex.Message)