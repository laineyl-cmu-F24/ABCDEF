module CrossTradedCurrencyPair
open FSharp.Data
open System

type TradingParameters = {
    NumOfCrypto: int
    MinSpreadPrice: decimal
    MinTransactionProfit: decimal
    MaxTransactionValue: decimal
    MaxTradeValue: decimal
    InitialInvestmentAmount: decimal
    Email: string option
    PnlThreshold: decimal option
}

// Filter unction to select valid pairs
let isValidPair (pair: string) (separator: string) =
    let parts = pair.Split( separator , StringSplitOptions.RemoveEmptyEntries)
    parts.Length = 2 && parts.[0].Length = 3 && parts.[1].Length = 3

let convertPair (pair: string) (separator: string) =
    let parts = pair.Split( separator , StringSplitOptions.RemoveEmptyEntries)
    parts[0].ToUpper() + "-" + parts[1].ToUpper()


(* ----- Bitfinex ----- *)
type BitfinexPairs = JsonProvider<"https://api-pub.bitfinex.com/v2/conf/pub:list:pair:exchange">
let getBitfinexPairs =
    BitfinexPairs.GetSamples()
    |> Seq.concat

let processBitfinexPairs (bitfinexData: seq<string>) =
    bitfinexData
    |> Seq.map (fun pair ->
        match pair.Split(':') with
        | [| _; _ |] -> pair // Already in "currency1:currency2" format, keep as is
        | [| single |] when single.Length = 6 ->
            // Convert to "currency1:currency2" format
            single.Substring(0, 3) + ":" + single.Substring(3, 6)
        | _ -> "" // Invalid format, output empty string to be filtered out
    )
    |> Seq.filter (fun pair -> pair <> "" && isValidPair pair ":") // Remove invalid pairs
    |> Seq.map (convertPair ":")
    |> Seq.distinct

(* ----- Bitstamp ----- *) 
type BitstampPairs = JsonProvider<"https://www.bitstamp.net/api/v2/ticker/">
let getBitstampPairs =
    BitstampPairs.GetSamples()
    |> Array.toSeq
// Function to process Bitstamp pairs
let processBitstampPairs (bitstampData: seq<BitstampPairs.Root>) =
    bitstampData
    |> Seq.map (fun data -> data.Pair)
    |> Seq.filter (isValidPair "/")
    |> Seq.map (convertPair "/")
    |> Seq.distinct


(* ----- Kraken ----- *)
// There is an extra "error" empty field in Kraken response, so it needs additional process
type KrakenPairs = JsonProvider<"https://api.kraken.com/0/public/AssetPairs">
type KrakenElem = JsonProvider<"""
    {"altname":"1INCHEUR","wsname":"1INCH/EUR","aclass_base":"currency","base":"1INCH","aclass_quote":"currency",
    "quote":"ZEUR","lot":"unit","cost_decimals":5,"pair_decimals":3,"lot_decimals":8,"lot_multiplier":1,
    "leverage_buy":[],"leverage_sell":[],"fees":[[0,0.4],[10000,0.35],[50000,0.24],[100000,0.22],[250000,0.2],[500000,0.18],[1000000,0.16],[2500000,0.14],[5000000,0.12],[10000000,0.1]],
    "fees_maker":[[0,0.25],[10000,0.2],[50000,0.14],[100000,0.12],[250000,0.1],[500000,0.08],[1000000,0.06],[2500000,0.04],[5000000,0.02],[10000000,0]],
    "fee_volume_currency":"ZUSD","margin_call":80,"margin_stop":40,"ordermin":"11","costmin":"0.45","tick_size":"0.001","status":"online"}
    """>
// Function to load and parse Kraken pairs, accessing only the "result" field
let getKrakenPairs =
    let response = Http.RequestString("https://api.kraken.com/0/public/AssetPairs")
    let parsed = KrakenPairs.Parse(response).JsonValue
    match parsed.TryGetProperty("result") with
        | Some result -> 
            result.Properties() 
            |> Array.map snd
            |> Array.toSeq
            |> Seq.map (fun data ->
                            let parsed = KrakenElem.Parse(data.ToString())
                            parsed)
        | None -> Seq.empty
    
let processKrakenPairs (krakenData: seq<KrakenElem.Root>) =
    krakenData
    |> Seq.map (fun data -> data.Wsname)
    |> Seq.filter (isValidPair "/")
    |> Seq.map (convertPair "/")
    |> Seq.distinct

let findCrossTradedPairs (bitfinexPairs: seq<string>) (bitstampPairs: seq<string>) (krakenPairs: seq<string>) =
    // Convert each sequence to a set for efficient comparison
    let allPairs = Seq.concat [bitfinexPairs; bitstampPairs; krakenPairs]
    allPairs
    |> Seq.countBy id
    |> Seq.filter (fun (_, count) -> count >= 2)
    |> Seq.map fst

let processedBitfinexPairs = processBitfinexPairs getBitfinexPairs
let processedBitstampPairs = processBitstampPairs getBitstampPairs
let processedKrakenPairs = processKrakenPairs getKrakenPairs

let res = findCrossTradedPairs processedBitfinexPairs processedBitstampPairs processedKrakenPairs
printfn "%A" res



    
    
    




