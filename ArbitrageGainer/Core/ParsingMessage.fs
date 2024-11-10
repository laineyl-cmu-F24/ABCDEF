module Core.ParsingMessage

open System
open System.Text.Json
open Core.Models

let parseMessage(json: string) : ParseResult =
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
                    let timestamp = message.GetProperty("t").GetInt64()
                    let quote = {
                        Symbol =  pair
                        Exchange = exchangeId.ToString()
                        BidPrice = bidPrice
                        AskPrice = askPrice
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime
                    }
                    QuoteReceived quote
                |_ ->
                    ParseError "Skipping quote from unsupported exchange"
            |"status" ->
                let status = message.GetProperty("status").GetString()
                let statusMessage = message.GetProperty("message").GetString()
                let statusMsg = { ev = "status"; status = status; message = statusMessage }
                StatusReceived statusMsg
            |_ ->
                ParseError $"Unknown or unsupported event type: {ev}"
        |false ->
            ParseError "Empty message array"
    with ex ->
        ParseError ex.Message