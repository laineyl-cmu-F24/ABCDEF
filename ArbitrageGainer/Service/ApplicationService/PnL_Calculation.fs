module Service.ApplicationService.PnL

open System
open Core.Model.Models
open Infrastructure.Repository.DatabaseInterface

type PnLState = {
    TotalPnL: decimal
}

// Initial PnL State
let initialPnLState = {
    TotalPnL = 0M
}

type PnLMessage =
    | AddTransaction of Transaction
    | ResetAgent
    | GetHistoricalPnL of DateTime * DateTime * AsyncReplyChannel<decimal option>
    | GetCurrentPnL of AsyncReplyChannel<decimal>

// Calculate the P&L of a transaction
let calculatePLofTransaction (transaction: Transaction) =
    match transaction.Side with
    | Sell -> transaction.Amount * transaction.Price
    | Buy -> -transaction.Amount * transaction.Price

// Calculate the cumulative P&L of a list of transactions
let calculatePLofTransactions (transactions: List<Transaction>) =
    transactions |> List.sumBy calculatePLofTransaction

// Calculate the cumulative P&L within a user-specified period
let calculatePLofWithinTime (starting: DateTime) (ending: DateTime) =
    match getTransactionWithinTime starting ending with
    | Ok transactions ->
        let fsharpTransactions = transactions |> Seq.toList // Convert to F# list
        let totalPL = fsharpTransactions |> List.sumBy calculatePLofTransaction
        Ok totalPL
    | Error err -> Error err

let PnLAgent = MailboxProcessor<PnLMessage>.Start(fun inbox ->
    let rec loop state =
        async {
            let! message = inbox.Receive()
            match message with
            | AddTransaction transaction ->
                let pl = calculatePLofTransaction transaction
                let newTotalPnL = state.TotalPnL + pl
                return! loop { state with TotalPnL = newTotalPnL }
            | GetCurrentPnL reply ->
                reply.Reply(state.TotalPnL)
                return! loop state
            | GetHistoricalPnL (starting, ending, reply) ->
                match calculatePLofWithinTime starting ending with
                | Ok pl -> reply.Reply(Some pl)  // Reply with the calculated P&L
                | Error _ -> reply.Reply(None)    // Reply with None in case of error
                return! loop state
            | ResetAgent ->
                return! loop initialPnLState
        }
    loop initialPnLState
)

// APIs of PnL Agent

// Record the P&L
let addTransaction (transaction: Transaction) =
    PnLAgent.Post(AddTransaction transaction)

// Get Current P&L
let getCurrentPnL =
    PnLAgent.PostAndAsyncReply(GetCurrentPnL)

// Get Historical P&L within a specified time frame
let getHistoricalPnLWithIn (starting: DateTime) (ending: DateTime) =
    PnLAgent.PostAndAsyncReply(fun reply -> GetHistoricalPnL(starting, ending, reply))

// Reset the PnL Agent
let resetPnLAgent =
    PnLAgent.Post(ResetAgent)

