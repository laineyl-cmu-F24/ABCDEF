module Service.ApplicationService.CrossTradedCurrencyPair
open FSharp.Data
open System
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface
open Infrastructure.Client.ModuleAPI

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




    
    
    




