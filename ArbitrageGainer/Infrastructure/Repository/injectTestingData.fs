module TestDataInjector

open System
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface

// Define Test Transactions
let testTransactions = [
    {
        Id = "txn-001"
        OrderId = "order-001"
        Exchange = Bitfinex
        Symbol = "BTC-USD"
        Side = Buy
        Price = 45000m
        Amount = 0.5m
        Timestamp = DateTime.UtcNow.AddMinutes(-30.0)
    }
    {
        Id = "txn-002"
        OrderId = "order-002"
        Exchange = Kraken
        Symbol = "ETH-USD"
        Side = Sell
        Price = 3000m
        Amount = 2.0m
        Timestamp = DateTime.UtcNow.AddMinutes(-15.0)
    }
    {
        Id = "txn-003"
        OrderId = "order-003"
        Exchange = Bitstamp
        Symbol = "BTC-USD"
        Side = Buy
        Price = 46000m
        Amount = 1.0m
        Timestamp = DateTime.UtcNow
    }
]

// Insert Test Transactions into the Database
let insertTestTransactions () =
    testTransactions
    |> List.iter (fun transaction ->
        match saveTransaction transaction with
        | Ok () -> printfn "Inserted transaction: %A" transaction.Id
        | Error (DatabaseError msg) -> printfn "Failed to insert transaction: %s" msg
    )

// Verify Insertions
let verifyInsertedTransactions () =
    match getTransactions () with
    | Ok transactions ->
        printfn "Retrieved %d transactions:" transactions.Count
        transactions |> List.iter (fun t -> printfn "%A" t)
    | Error (DatabaseError msg) ->
        printfn "Failed to retrieve transactions: %s" msg

// Entry Point for Running the Script
let run () =
    printfn "Inserting test transactions..."
    insertTestTransactions ()
    printfn "Verifying inserted transactions..."
    verifyInsertedTransactions ()
