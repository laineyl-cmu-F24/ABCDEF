module M2.Core.ArbitrageAnalysis

open M2.Core.Models

// Retrieve historical arbitrage data
let retrieveHistoricalData (data) : Result<CryptoArbitrage list, DomainError> =
    // TODO: Implement actual data retrieval from your data source
    Ok [
            { Symbol = CryptoSymbol "BTC-USD"; OpportunityCount = ArbitrageOpportunityCount 150 }
            { Symbol = CryptoSymbol "ETH-USD"; OpportunityCount = ArbitrageOpportunityCount 120 }
            { Symbol = CryptoSymbol "LTC-USD"; OpportunityCount = ArbitrageOpportunityCount 90 }
            { Symbol = CryptoSymbol "XRP-USD"; OpportunityCount = ArbitrageOpportunityCount 80 }
            { Symbol = CryptoSymbol "BCH-USD"; OpportunityCount = ArbitrageOpportunityCount 70 }
        ]

//Function to get top N cryptocurrencies based on arbitrage opportunities
let getTopNCurrencies N cryptos =
    cryptos
    |> List.sortByDescending (fun c -> let (ArbitrageOpportunityCount count) = c.OpportunityCount in count)
    |> List.truncate(N)

// Function to filter cross-traded cryptocurrencies
let filterCrossTraded (crossTraded: Set<CryptoSymbol>) (cryptos: CryptoArbitrage List) =
    cryptos
    |> List.map(fun c -> c.Symbol)
    |> List.filter(fun symbol -> crossTraded.Contains(symbol))
    