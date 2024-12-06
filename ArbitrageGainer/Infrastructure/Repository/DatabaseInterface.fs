module Infrastructure.Repository.DatabaseInterface
open System
open MongoDB.Driver
open MongoDB.Bson
open Core.Model.Models
open Logging.Logger


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
    
let orderCollection = db.GetCollection<Order>("orders")
let saveOrder order =
    try
        let _ = orderCollection.InsertOne(order)
        Ok ()
    with
    | ex -> Error (DatabaseError ex.Message)

let transactionCollection = db.GetCollection<Transaction>("transactions")

// Reusable Error Handling
let tryDbOperation (operation: unit -> 'T) (args: 'Args) : DomainResult<'T> =
    try
        Ok (operation args)
    with
    | ex -> Error (DatabaseError ex.Message)

let saveTransaction (transaction: Transaction) =
    tryDbOperation (fun () -> transactionCollection.InsertOne(transaction))

let getTransactions () =
    tryDbOperation (fun () -> transactionCollection.Find(Builders<Transaction>.Filter.Empty).ToList())

let getTransactionByOrderId (orderId: string) =
    let filter = Builders<Transaction>.Filter.Eq((fun t -> t.OrderId), orderId)
    tryDbOperation (fun () -> transactionCollection.Find(filter).ToList())

let getTransactionWithinTime (startTime:DateTime) (endTime:DateTime) =
    let filter =
        Builders<Transaction>.Filter.And(
            Builders<Transaction>.Filter.Gte((fun t -> t.Timestamp), startTime),
            Builders<Transaction>.Filter.Lte((fun t -> t.Timestamp), endTime)
        )
    tryDbOperation (fun () -> transactionCollection.Find(filter).ToList()) ()
