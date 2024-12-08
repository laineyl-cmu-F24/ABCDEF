module Infrastructure.Repository.DatabaseInterface
open System
open MongoDB.Driver
open MongoDB.Bson
open Core.Model.Models


// DB item definition
type CurrencyPair = {
    Id: ObjectId // Auto-generated IDs
    name: string
}

// DB configuration
// Lazy DB configuration to avoid type initialization issues
let connString = "mongodb+srv://ranf:functional123@arbitragegainer.hlllv.mongodb.net/?retryWrites=true&w=majority&appName=ArbitrageGainer"

let lazyClient = lazy (
    try
        new MongoClient(connString)
    with
    | ex ->
        printfn "MongoDB initialization error: %s" ex.Message
        raise ex
)



let db = lazyClient.Value.GetDatabase("product")
let currencyPairsCollection = db.GetCollection<CurrencyPair>("currency_pairs")
// let db = client.GetDatabase("product")
// let currencyPairsCollection = db.GetCollection<CurrencyPair>("currency_pairs")
let createCurrencyPair (pairName: string) =
    try
        let newCurrencyPair = { Id = ObjectId.GenerateNewId(); name = pairName }
        let _ = currencyPairsCollection.InsertOne(newCurrencyPair)
        // printfn "Inserted currency pair: %A" createRes
        Ok ()
    with
    | ex -> Error (DatabaseError ex.Message)

let getCurrencyPair() =
    let currencyPairs = 
        currencyPairsCollection.Find(FilterDefinition<CurrencyPair>.Empty)
        |> fun cursor -> cursor.ToList()

    let names = 
        currencyPairs 
        |> Seq.map (fun cp -> cp.name) 
        |> Set.ofSeq
    names
    
let orderCollection = db.GetCollection<Order>("orders")
let saveOrder order =
    try
        let _ = orderCollection.InsertOne(order)
        Ok ()
    with
    | ex -> Error (DatabaseError ex.Message)

let transactionCollection = db.GetCollection<TransactionDB>("transactions")

// Helper functions for Exchange
let exchangeToString (exchange: Exchange) =
    match exchange with
    | Bitfinex -> "Bitfinex"
    | Kraken -> "Kraken"
    | Bitstamp -> "Bitstamp"

let stringToExchange (str: string) =
    match str with
    | "Bitfinex" -> Bitfinex
    | "Kraken" -> Kraken
    | "Bitstamp" -> Bitstamp
    | _ -> failwith "Invalid exchange value"

// Helper functions for Side
let sideToString (side: Side) =
    match side with
    | Buy -> "Buy"
    | Sell -> "Sell"

let stringToSide (str: string) =
    match str with
    | "Buy" -> Buy
    | "Sell" -> Sell
    | _ -> failwith "Invalid side value"

// Convert Transaction to TransactionDB
let transactionToTransactionDB (transaction: Transaction): TransactionDB = {
    Id = transaction.Id
    OrderId = transaction.OrderId
    Exchange = exchangeToString transaction.Exchange
    Symbol = transaction.Symbol
    Side = sideToString transaction.Side
    Price = transaction.Price
    Amount = transaction.Amount
    Timestamp = transaction.Timestamp
}

// Convert TransactionDB to Transaction
let transactionDBToTransaction (transactionDB: TransactionDB): Transaction = {
    Id = transactionDB.Id
    OrderId = transactionDB.OrderId
    Exchange = stringToExchange transactionDB.Exchange
    Symbol = transactionDB.Symbol
    Side = stringToSide transactionDB.Side
    Price = transactionDB.Price
    Amount = transactionDB.Amount
    Timestamp = transactionDB.Timestamp
}

// Reusable Error Handling
// Reusable Error Handling
let tryDbOperation (operation: unit -> 'T) : DomainResult<'T> =
    try
        Ok (operation ())
    with
    | ex -> Error (DatabaseError ex.Message)

let saveTransaction (transaction: Transaction) =
    let transactionDB = transactionToTransactionDB transaction
    tryDbOperation (fun () ->
        transactionCollection.InsertOne(transactionDB)
        transactionDB // Return the saved document for confirmation
    )

let getTransactions () =
    tryDbOperation (fun () -> 
        transactionCollection
            .Find(Builders<TransactionDB>.Filter.Empty)
            .ToList() // Returns a .NET List<TransactionDB>
            .ToArray() // Convert to array
            |> Array.toList // Convert to F# list
            |> List.map transactionDBToTransaction // Map to domain type
    )

let getTransactionByOrderId (orderId: string) =
    let filter = Builders<TransactionDB>.Filter.Eq("OrderId", orderId)
    tryDbOperation (fun () -> 
        transactionCollection
            .Find(filter)
            .ToList()
            .ToArray()
            |> Array.toList
            |> List.map transactionDBToTransaction
    )

let getTransactionWithinTime (startTime: DateTime) (endTime: DateTime) =
    let filter =
        Builders<TransactionDB>.Filter.And(
            Builders<TransactionDB>.Filter.Gte("Timestamp", startTime),
            Builders<TransactionDB>.Filter.Lte("Timestamp", endTime)
        )
    tryDbOperation (fun () -> 
        transactionCollection
            .Find(filter)
            .ToList()
            .ToArray()
            |> Array.toList
            |> List.map transactionDBToTransaction
    )


    
let historicalArbitrageOpportunity = db.GetCollection<TradeRecord>("historicalArbitrageOpportunities")

let saveHistoricalArbitrageOpportunity (opportunity: TradeRecord) =
    try
        historicalArbitrageOpportunity.InsertOne(opportunity)
        Ok ()
    with
    | ex -> Error (DatabaseError ex.Message)

let getHistoricalOpportunity () =
    historicalArbitrageOpportunity
        .Find(Builders<TradeRecord>.Filter.Empty)
        .ToList()
    |> Seq.toList


    