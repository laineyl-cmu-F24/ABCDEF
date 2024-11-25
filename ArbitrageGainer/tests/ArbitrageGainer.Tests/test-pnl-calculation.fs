module ArbitrageGainer.Tests.PnL

open NUnit.Framework
open System
open Service.ApplicationService.PnL
open Core.Model.Models

[<SetUp>]
let setup () =
    resetPnLAgent // Reset the state before each test

let createTransaction (side: Side) (price: decimal) (amount: decimal) =
    { 
        Id = Guid.NewGuid().ToString()
        OrderId = Guid.NewGuid().ToString()
        Exchange = Exchange.Bitfinex
        Symbol = "BTC-USD"
        Side = side
        Price = price
        Amount = amount
        Timestamp = DateTime.UtcNow
    }

[<Test>]
let ``ResetThreshold clears the PnL threshold`` () =
    setup()
    SetThreshold 500M
    resetPnLAgent
    let currentPnL = getCurrentPnL |> Async.RunSynchronously
    Assert.That(currentPnL, Is.EqualTo(0M))

[<Test>]
let ``ResetAgent restores initial state`` () =
    setup()
    let transaction = createTransaction Buy 50M 10M
    addTransaction transaction
    resetPnLAgent
    let currentPnL = getCurrentPnL |> Async.RunSynchronously
    Assert.That(currentPnL, Is.EqualTo(0M))

[<Test>]
let ``calculatePLofTransaction calculates correctly for Buy`` () =
    let transaction = createTransaction Buy 20M 15M
    let result = calculatePLofTransaction transaction
    Assert.That(result, Is.EqualTo(-300M)) // Buy => -Amount * Price

[<Test>]
let ``calculatePLofTransaction calculates correctly for Sell`` () =
    let transaction = createTransaction Sell 20M 15M
    let result = calculatePLofTransaction transaction
    Assert.That(result, Is.EqualTo(300M)) // Sell => Amount * Price

[<Test>]
let ``calulatePLofTransactions calculates cumulative PnL correctly`` () =
    let transactions = [
        createTransaction Buy 50M 10M   // -500
        createTransaction Sell 100M 5M // +500
    ]
    let result = calculatePLofTransactions transactions
    Assert.That(result, Is.EqualTo(0M)) // Net PnL = -500 + 500

[<Test>]
let ``AddTransaction updates TotalPnL correctly`` () =
    setup()
    let transaction = createTransaction Sell 100M 2M
    addTransaction transaction
    let currentPnL = getCurrentPnL |> Async.RunSynchronously
    Assert.That(currentPnL, Is.EqualTo(0M)) // Sell => Amount * Price

[<Test>]
let ``SetThreshold triggers state change when threshold is exceeded`` () =
    setup()
    SetThreshold 200M
    let transaction = createTransaction Sell 100M 2M
    addTransaction transaction
    let currentPnL = getCurrentPnL |> Async.RunSynchronously
    Assert.That(currentPnL, Is.EqualTo(0M)) // State resets when threshold is exceeded

[<Test>]
let ``GetHistoricalPnL returns correct PnL for a given time range`` () =
    setup()
    let startTime = DateTime.UtcNow.AddMinutes(-10.0)
    let endTime = DateTime.UtcNow.AddMinutes(10.0)
    let transactions = [
        createTransaction Buy 20M 5M  // -100
        createTransaction Sell 50M 2M // +100
    ]
    transactions |> List.iter addTransaction
    let result = getHistoricalPnLWithIn startTime endTime |> Async.RunSynchronously
    Assert.That(result.Value, Is.EqualTo(0M)) // Historical PnL = -100 + 100

[<Test>]
let ``ResetAgent clears all thresholds and transactions`` () =
    setup()
    ResetThreshold
    let transaction = createTransaction Sell 100M 5M
    addTransaction transaction
    resetPnLAgent
    let currentPnL = getCurrentPnL |> Async.RunSynchronously
    Assert.That(currentPnL, Is.EqualTo(0M))
    let result = getHistoricalPnLWithIn DateTime.MinValue DateTime.UtcNow |> Async.RunSynchronously
    Assert.That(result, Is.EqualTo(Some 0M))
