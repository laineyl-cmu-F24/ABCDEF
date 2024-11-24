module Service.ApplicationService.CrossTradedCurrencyPair
open FSharp.Data
open System
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface
open Infrastructure.Client.ModuleAPI

// Filter unction to select valid pairs
let isValidPair (separator: string) (pair: string) =
    let parts =
        pair.Split( separator , StringSplitOptions.RemoveEmptyEntries)
    // printfn "parts %A" parts
    parts.Length = 2 && parts[0].Length = 3 && parts[1].Length = 3

let convertPair (separator: string) (pair: string) =
    let parts = pair.Split( separator , StringSplitOptions.RemoveEmptyEntries)
    parts[0].ToUpper() + "-" + parts[1].ToUpper()
    

(* ----- Bitfinex ----- *)
let getBitfinexPairs =
    try
        let samples = BitfinexPairs.GetSamples()
        Ok (Seq.concat samples)
    with
    | ex -> Error (PairParseError $"Error parsing Bitfinex pairs: {ex.Message}")


let processBitfinexPairs (bitfinexData: seq<string>) =
    bitfinexData
    |> Seq.map (fun pair ->
        match pair.Split(':') with
        | [| _; _ |] -> pair // Already in "currency1:currency2" format, keep as is
        | [| single |] when single.Length = 6 ->
            // Convert to "currency1:currency2" format
            single[0..2] + ":" + single[3..5]
        | _ -> "" // Invalid format, output empty string to be filtered out
    )
    |> Seq.filter (fun pair -> pair <> "" && isValidPair pair ":") // Remove invalid pairs
    |> Seq.map (convertPair ":")
    |> Seq.filter (fun converted -> converted <> "")
    |> Seq.distinct

(* ----- Bitstamp ----- *) 
let getBitstampPairs =
    try
        let samples = BitstampPairs.GetSamples()
        Ok (Array.toSeq samples)
    with
    | ex -> Error (PairParseError $"Error parsing Bitstamp pairs: {ex.Message}")
    
// Function to process Bitstamp pairs
let processBitstampPairs (bitstampData: seq<BitstampPairs.Root>) =
    bitstampData
    |> Seq.map (fun data -> data.Pair)
    |> Seq.filter (isValidPair "/")
    |> Seq.map (convertPair "/")
    |> Seq.filter (fun converted -> converted <> "")
    |> Seq.distinct


(* ----- Kraken ----- *)
// Function to load and parse Kraken pairs, accessing only the "result" field
let getKrakenPairs =
    // let response = Http.RequestString("https://api.kraken.com/0/public/AssetPairs")
    let parseKrakenResponse (responseBody: string) =
        try
            let parsed = KrakenPairs.Parse(responseBody).JsonValue
            Ok parsed
        with
        | ex -> Error (PairParseError $"Error parsing Kraken response: {ex.Message}")
    let parseKrakenElement (dataElem: string) =
        try
            let parsedData = KrakenElem.Parse(dataElem)
            Ok parsedData
        with
        | ex -> Error (PairParseError $"Error parsing Kraken data element: {ex.Message}")
    let response =
        try
            Ok (Http.RequestString("https://api.kraken.com/0/public/AssetPairs"))
        with
        | ex -> 
            Error (SubscriptionError $"Error requesting data: {ex.Message}")
    match response with
    | Ok responseBody ->
        // Parse the JSON response
        match parseKrakenResponse responseBody with
        | Ok parsed ->
            match parsed.TryGetProperty("result") with
            | Some result -> 
                result.Properties() 
                |> Array.map snd
                |> Array.toSeq
                |> Seq.map (fun data ->
                    match parseKrakenElement (data.ToString()) with
                    | Ok resultData -> Some resultData
                    | Error _ -> None
                )
            | None -> 
                printfn "No 'result' property found in parsed response."
                Seq.empty
        | Error _ ->
            printfn "Kraken http response body parse error"
            Seq.empty
    | Error _ ->
        printfn "Kraken url load error"
        Seq.empty
         
    
let processKrakenPairs (krakenData: seq<KrakenElem.Root option>) =
    krakenData
    |> Seq.choose id
    |> Seq.map (fun data -> data.Wsname)
    |> Seq.filter (isValidPair "/")
    |> Seq.map (convertPair "/")
    |> Seq.filter (fun converted -> converted <> "")
    |> Seq.distinct

let findCrossTradedPairs (bitfinexPairs: seq<string>) (bitstampPairs: seq<string>) (krakenPairs: seq<string>) =
    // Convert each sequence to a set for efficient comparison
    let allPairs = Seq.concat [bitfinexPairs; bitstampPairs; krakenPairs]
    allPairs
    |> Seq.countBy id
    |> Seq.filter (fun (_, count) -> count >= 2)
    |> Seq.map fst

let findCurrencyPairs =
    // Chain the calls in a railway-oriented style
    match getBitfinexPairs with
    | Ok bitfinexParseResult ->
        let processedBitfinexPairs = processBitfinexPairs bitfinexParseResult
        match getBitstampPairs with
        | Ok bitstampParseResult ->
            let processedBitstampPairs = processBitstampPairs bitstampParseResult
            match getKrakenPairs with
            | krakenParseResult ->
                let processedKrakenPairs = processKrakenPairs krakenParseResult
                let res = findCrossTradedPairs processedBitfinexPairs processedBitstampPairs processedKrakenPairs
                res |> Seq.iter (fun pairName ->
                        match createCurrencyPair pairName with
                            | Ok _ -> ()
                            | Error err -> printfn "Failed to save currency pair: %s. Error: %A" pairName err
                        )
                printfn "currency pairs: %A" res
                res
        | Error _ ->
            printfn "Cross-currency pair processing error"
            Seq.empty
    | Error _ ->
        printfn "Cross-currency pair processing error"
        Seq.empty




    
    
    




