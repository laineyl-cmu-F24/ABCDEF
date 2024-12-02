module Service.ApplicationService.Workflow
open Core.Model.Models

let runTradingWorkflow (tradingParams: TradingParameters) (crossTradedCryptos: Set<string>) (tradeHistory: TradeRecord list)=
    // Step 1: Get top N aarbitrage opportunities cryptocurrencies 
    let topNCryptosResult =
        tradeHistory
        |> List.sortByDescending (fun record -> record.OpportunityCount)
        |> List.truncate tradingParams.NumOfCrypto
        |> List.map (fun record -> record.Pair)
    
    match List.length tradeHistory with
        | count when count < tradingParams.NumOfCrypto ->
            printfn "Warning: Requested top %d cryptos, but only %d available in trade history." tradingParams.NumOfCrypto count
        | _ -> ()
    
    // Step 2: Check with cross-traded cryptocurrencies
    let filteredCryptos =
        topNCryptosResult
        |> List.filter crossTradedCryptos.Contains
    
    filteredCryptos
        
        
    
    
    

