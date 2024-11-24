module Infrastructure.Client.ModuleAPI

open System
open System.Net.Http
open FSharp.Data
open System.Text
open System.Threading.Tasks
open Newtonsoft.Json
open Infrastructure.Repository.DatabaseInterface
open Core.Model.Models

let httpClient = new HttpClient()

type BitfinexPairs = JsonProvider<"https://api-pub.bitfinex.com/v2/conf/pub:list:pair:exchange">
type BitstampPairs = JsonProvider<"https://www.bitstamp.net/api/v2/ticker/">
type KrakenPairs = JsonProvider<"https://api.kraken.com/0/public/AssetPairs">
type KrakenElem = JsonProvider<"""
    {"altname":"1INCHEUR",
     "wsname":"1INCH/EUR","aclass_base":"currency","base":"1INCH","aclass_quote":"currency",
    "quote":"ZEUR","lot":"unit","cost_decimals":5,"pair_decimals":3,"lot_decimals":8,"lot_multiplier":1,
    "leverage_buy":[],"leverage_sell":[],
    "fees":[[0,0.4],[10000,0.35],[50000,0.24],[100000,0.22],[250000,0.2],[500000,0.18],[1000000,0.16],[2500000,0.14],[5000000,0.12],[10000000,0.1]],
    "fees_maker":[[0,0.25],[10000,0.2],[50000,0.14],[100000,0.12],[250000,0.1],[500000,0.08],[1000000,0.06],[2500000,0.04],[5000000,0.02],[10000000,0]],
    "fee_volume_currency":"ZUSD","margin_call":80,"margin_stop":40,"ordermin":"11","costmin":"0.45",
    "tick_size":"0.001","status":"online"}
    """>

let bitfinexSubmitOrderUrl = "https://api.bitfinex.com/v2/auth/w/order/submit"
let bitfinexRetrieveOrderTradesUrl = "https://api.bitfinex.com/v2/auth/r/order/{symbol}:{id}/trades"

let krakenSubmitOrderUrl = "https://api.kraken.com/0/private/AddOrder"
let krakenQueryOrderInfoUrl = "https://api.kraken.com/0/private/QueryOrders"

let bitstampEmitBuyOrderUrl = "https://www.bitstamp.net/api/v2/buy/market_order/"
let bitstampEmitSellOrderUrl = "https://www.bitstamp.net/api/v2/sell/market_order/"
let bitstampRetrieveOrderStatusUrl = "https://www.bitstamp.net/api/v2/order_status/"

let submitBitfinexOrder (order: Order) : Task<Order> = task {
    let requestBody = {
        symbol = order.Symbol
        amount = order.Amount
        price = order.Price
        exchange = "bitfinex"
        side = match order.Side with | Buy -> "buy" | Sell -> "sell"
        ``type`` = "exchange market"
    }

    let json = JsonConvert.SerializeObject(requestBody)
    let content = new StringContent(json, Encoding.UTF8, "application/json")

    let! response = httpClient.PostAsync(bitfinexSubmitOrderUrl, content)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let submitResponse = JsonConvert.DeserializeObject<BitfinexSubmitOrderResponse>(responseBody)

    let updatedOrder = { order with OrderId = submitResponse.id }
    
    let result = saveOrder updatedOrder
    printfn $"Submitted Bitfinex order: %A{updatedOrder}"
    return updatedOrder
}

let retrieveBitfinexOrderStatus (order: Order) : Task<OrderStatus> = task {
    let requestBody = { order_id = order.OrderId }
    let json = JsonConvert.SerializeObject(requestBody)
    let content = new StringContent(json, Encoding.UTF8, "application/json")

    let! response = httpClient.PostAsync(bitfinexRetrieveOrderTradesUrl, content)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let trades = JsonConvert.DeserializeObject<BitfinexRetrieveOrderTradesResponse list>(responseBody)

    let fulfilledAmount = trades |> List.sumBy (fun trade -> trade.amount)
    let remainingAmount = order.Amount - fulfilledAmount
    let status = if remainingAmount = 0m then "FullyFilled" else "PartiallyFilled"

    let orderStatus = {
        OrderId = order.OrderId
        FulfilledAmount = fulfilledAmount
        RemainingAmount = remainingAmount
        Status = status
    }

    printfn $"Retrieved Bitfinex order status: %A{orderStatus}"
    return orderStatus
}

let submitKrakenOrder (order: Order) : Task<Order> = task {
    let requestBody = {
        pair = order.Symbol
        ``type`` = match order.Side with | Buy -> "buy" | Sell -> "sell"
        ordertype = "market"
        volume = order.Amount
        price = order.Price
    }

    let json = JsonConvert.SerializeObject(requestBody)
    let content = new StringContent(json, Encoding.UTF8, "application/json")

    let! response = httpClient.PostAsync(krakenSubmitOrderUrl, content)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let submitResponse = JsonConvert.DeserializeObject<KrakenSubmitOrderResponse>(responseBody)

    let orderId = List.head submitResponse.txid
    let updatedOrder = { order with OrderId = orderId }
    let result = saveOrder updatedOrder
    printfn $"Submitted Kraken order: %A{updatedOrder}"
    return updatedOrder
}

let retrieveKrakenOrderStatus (order: Order) : Task<OrderStatus> = task {
    let requestBody = { txid = [ order.OrderId ] }
    let json = JsonConvert.SerializeObject(requestBody)
    let content = new StringContent(json, Encoding.UTF8, "application/json")

    let! response = httpClient.PostAsync(krakenQueryOrderInfoUrl, content)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let trades = JsonConvert.DeserializeObject<KrakenRetrieveOrderTradesResponse list>(responseBody)

    let fulfilledAmount = trades |> List.sumBy (fun trade -> trade.amount)
    let remainingAmount = order.Amount - fulfilledAmount
    let status = if remainingAmount = 0m then "FullyFilled" else "PartiallyFilled"

    let orderStatus = {
        OrderId = order.OrderId
        FulfilledAmount = fulfilledAmount
        RemainingAmount = remainingAmount
        Status = status
    }

    printfn $"Retrieved Bitfinex order status: %A{orderStatus}"
    return orderStatus
}

let emitBitstampOrder (order: Order) : Task<Order> = task {
    let url =
        match order.Side with
        | Buy -> bitstampEmitBuyOrderUrl
        | Sell -> bitstampEmitSellOrderUrl

    let requestBody = {
        amount = order.Amount
        price = order.Price
    }

    let json = JsonConvert.SerializeObject(requestBody)
    let content = new StringContent(json, Encoding.UTF8, "application/json")

    let! response = httpClient.PostAsync(url, content)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let emitResponse = JsonConvert.DeserializeObject<BitstampEmitOrderResponse>(responseBody)

    let updatedOrder = { order with OrderId = emitResponse.id }
    let result = saveOrder updatedOrder
    printfn $"Emitted Bitstamp order: %A{updatedOrder}"
    return updatedOrder
}

let retrieveBitstampOrderStatus (order: Order) : Task<OrderStatus> = task {
    let url = $"{bitstampRetrieveOrderStatusUrl}{order.OrderId}/"

    let! response = httpClient.GetAsync(url)
    response.EnsureSuccessStatusCode() |> ignore

    let! responseBody = response.Content.ReadAsStringAsync()
    let statusResponse = JsonConvert.DeserializeObject<BitstampRetrieveOrderStatusResponse>(responseBody)

    let fulfilledAmount =
        match statusResponse.status.ToLower() with
        | "completed" -> statusResponse.amount
        | "partially_filled" -> statusResponse.amount / 2m
        | _ -> 0m

    let remainingAmount = statusResponse.amount - fulfilledAmount
    let status =
        if remainingAmount = 0m then "FullyFilled"
        elif fulfilledAmount > 0m then "PartiallyFilled"
        else "Unfilled"

    let orderStatus = {
        OrderId = order.OrderId
        FulfilledAmount = fulfilledAmount
        RemainingAmount = remainingAmount
        Status = status
    }

    printfn $"Retrieved Bitstamp order status: %A{orderStatus}"
    return orderStatus
}


