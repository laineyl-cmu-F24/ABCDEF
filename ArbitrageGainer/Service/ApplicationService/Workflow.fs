module Service.ApplicationService.Workflow

open Core.Model.Models

let runTradingWorkflow (tradingParams: TradingParameters) (crossTradedCryptos: Set<string>) (tradeHistory: TradeRecord list)=
    // Step 1: Get top N aarbitrage opportunities cryptocurrencies 
    let topNCryptosResult =
        tradeHistory
        |> List.sortByDescending (fun record -> record.OpportunityCount)
        |> List.truncate tradingParams.NumOfCrypto
        |> List.map (fun record -> record.Pair)
    
    // Step 2: Check with cross-traded cryptocurrencies
    let filteredCryptos =
        topNCryptosResult
        |> List.filter (fun pair -> crossTradedCryptos.Contains pair)
    
    filteredCryptos
        
        
    
    
    

