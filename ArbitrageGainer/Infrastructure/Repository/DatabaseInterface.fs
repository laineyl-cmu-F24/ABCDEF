module Infrastructure.Repository.DatabaseInterface
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
        // printfn "received name %A" pairName
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
let saveTransaction transaction =
    try
        let _ = transactionCollection.InsertOne(transaction)
        Ok ()
    with
    | ex -> Error (DatabaseError ex.Message)

