module ArbitrageGainer.Tests.Historical
open NUnit.Framework
open Historical

[<Test>]
let ``mapHistoricalData should group quotes by timestamp and currency pair`` () =
    let sampleData = [
        { ExchangeID = 1; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.0771M; ask = 0.0773M }
        { ExchangeID = 2; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.0772M; ask = 0.0774M }
        { ExchangeID = 1; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 30000M; ask = 30010M }
        { ExchangeID = 2; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 29990M; ask = 30005M }
    ]

    // Act
    let result = mapHistoricalData sampleData

    // Assert: Validate structure and content of mapping
    Assert.That(result |> Seq.length, Is.EqualTo(2)) // Two unique pairs
    let chzUsdQuotes = result |> Seq.find (fun (pair, _) -> pair = "CHZ-USD") |> snd
    Assert.That(chzUsdQuotes |> Seq.length, Is.EqualTo(2)) // Two exchanges for "CHZ-USD"
    let btcUsdQuotes = result |> Seq.find (fun (pair, _) -> pair = "BTC-USD") |> snd
    Assert.That(btcUsdQuotes |> Seq.length, Is.EqualTo(2)) // Two exchanges for "BTC-USD"

[<Test>]
let ``reduceHistoricalData should detect arbitrage opportunities`` () =
    // Arrange: Create sample mapped data with opportunities for testing
    let sampleMappedData = [
        "CHZ-USD", [
            (1, { ExchangeID = 1; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.08M; ask = 0.075M }, { ExchangeID = 1; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.08M; ask = 0.075M })
            (2, { ExchangeID = 2; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.077M; ask = 0.074M }, { ExchangeID = 2; CurrencyPair = "CHZ-USD"; TimeStamp = 1690409119847L; bid = 0.077M; ask = 0.074M })
        ]
        "BTC-USD", [
            (1, { ExchangeID = 1; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 30010M; ask = 29990M }, { ExchangeID = 1; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 30010M; ask = 29990M })
            (2, { ExchangeID = 2; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 29990M; ask = 29980M }, { ExchangeID = 2; CurrencyPair = "BTC-USD"; TimeStamp = 1690409119847L; bid = 29990M; ask = 29980M })
        ]
    ]

    // Act
    let result = reduceHistoricalData sampleMappedData

    // Assert: Validate arbitrage detection
    Assert.That(result |> Seq.length, Is.EqualTo(2)) // Two pairs with opportunities
    let chzUsdOpportunities = result |> Seq.find (fun (pair, _) -> pair = "CHZ-USD") |> snd
    Assert.That(chzUsdOpportunities, Is.EqualTo(1)) // Expected number of opportunities for "CHZ-USD"
    let btcUsdOpportunities = result |> Seq.find (fun (pair, _) -> pair = "BTC-USD") |> snd
    Assert.That(btcUsdOpportunities, Is.EqualTo(1)) // Expected number of opportunities for "BTC-USD"
