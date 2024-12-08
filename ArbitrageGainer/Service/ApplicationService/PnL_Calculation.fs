module Service.ApplicationService.PnL
open System
open Akka.Actor
open Akka.FSharp
open Core.Model.Models
open Suave
open Infrastructure.Repository.DatabaseInterface

type PnLState = {
    TotalPnL: decimal
    PnLThreshold: decimal option
    Email: string option
    Toggle: bool
}

// Initial PnL State
let initialPnLState = {
    TotalPnL = 0M
    PnLThreshold = None
    Email = None
    Toggle = true
}

// type PnLMessage =
//     | AddTransaction of Transaction
//     | SetThreshold of decimal
//     | ResetThreshold
//     | ResetAgent
//     | GetHistoricalPnL of DateTime * DateTime * AsyncReplyChannel<decimal option>
//     | GetCurrentPnL of AsyncReplyChannel<decimal>
//     | TogglePnl of bool

type PnLMessage =
    | AddTransaction of Transaction
    | SetThreshold of decimal
    | ResetThreshold
    | ResetAgent
    | GetHistoricalPnL of DateTime * DateTime * IActorRef
    | GetCurrentPnL of IActorRef
    | TogglePnl of bool

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

// manage the cumulative PnL
// Define the PnLActor
let createPnLActor system =
    spawn system "pnl-actor" <| fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | TogglePnl toggle ->
                    return! loop { state with Toggle = toggle }
                
                | AddTransaction transaction ->
                    match state.Toggle with
                    | true ->
                        let pl = calculatePLofTransaction transaction
                        let newTotalPnL = state.TotalPnL + pl
                        return! loop { state with TotalPnL = newTotalPnL }
                    | false ->
                        printfn "PnL calculation is toggled off."
                        return! loop state
                
                | GetCurrentPnL reply ->
                    // reply to the sender
                    mailbox.Sender() <! state.TotalPnL
                    return! loop state
                
                | GetHistoricalPnL (starting, ending, reply) ->
                    let result =
                        match calculatePLofWithinTime starting ending with
                        | Ok pl -> Some pl
                        | Error _ -> None
                    reply <! result // Reply directly to the provided actor reference
                    return! loop state
                
                | ResetAgent ->
                    return! loop initialPnLState
            }
        loop initialPnLState

// let PnLAgent = MailboxProcessor<PnLMessage>.Start(fun inbox ->
//     let rec loop state =
//         async {
//             let! message = inbox.Receive()
//             match message with
//             
//             | TogglePnl toggle ->
//                 return! loop {state with Toggle = toggle }
//             
//             | AddTransaction transaction ->
//                 match state.Toggle with
//                 | true -> 
//                     let pl = calculatePLofTransaction transaction
//                     // printfn $"calculatedPL %A{pl}"
//                     let newTotalPnL = state.TotalPnL + pl
//                     return! loop {state with TotalPnL = newTotalPnL}
//                 | _ ->
//                     printfn "PnL calculation is toggled off."
//                     return! loop state
//                
//             // | SetThreshold threshold ->
//             //     let newThreshold =
//             //         match threshold with
//             //         | 0m -> None
//             //         | _ -> Some threshold
//             //     return! loop{state with PnLThreshold = newThreshold }
//             
//             | GetCurrentPnL reply ->
//                 // printfn $"GETTTTT %A{state.TotalPnL}"
//                 reply.Reply(state.TotalPnL)
//                 return! loop state
//                 
//             | GetHistoricalPnL (starting, ending, reply) ->
//                 match calculatePLofWithinTime starting ending with
//                 | Ok pl -> reply.Reply(Some pl)  // Reply with the calculated P&L
//                 | Error _ -> reply.Reply(None)    // Reply with None in case of error
//                 return! loop state
//                 
//             | ResetAgent ->
//                 return! loop initialPnLState
//         }
//     loop initialPnLState
// )

// APIs of PnL Agent

// Record the P&L
let addTransaction (pnlActor: IActorRef) transaction =
    pnlActor <! AddTransaction transaction
// Get Current P&L
let getCurrentPnL (pnlActor: IActorRef) =
    async {
        let! result = pnlActor <? GetCurrentPnL
        return result
    }

// Get Historical P&L within a specified time frame
let getHistoricalPnLWithIn (pnlActor: IActorRef) (starting: DateTime) (ending: DateTime) =
    async {
        // Create a temporary actor to handle the response
        let! result =
            pnlActor <? GetHistoricalPnL(starting, ending, ActorRefs.NoSender) // Use <? for ask pattern
        return result
    }

// Reset the PnL Agent
let resetPnLAgent (pnlActor: IActorRef) =
    pnlActor <! ResetAgent

    
// Toggle PnL
let togglePnL (pnlActor: IActorRef) toggle =
    pnlActor <! TogglePnl toggle

let togglePnLHandler (pnlActor: IActorRef)  =
    fun (context: HttpContext) ->
        async {
            let req = context.request
            match req.formData "toggle" with
            | Choice1Of2 toggleStr ->
                match System.Boolean.TryParse(toggleStr) with
                | (true, toggle) ->
                    pnlActor <! TogglePnl toggle
                    return Successful.OK "PnL calculation toggle updated successfully\n", ()
                | _ ->
                    return RequestErrors.BAD_REQUEST "Invalid toggle value. Use 'true' or 'false'.\n", ()
            | _ -> 
                return RequestErrors.BAD_REQUEST "Missing 'toggle' parameter in the request.\n", ()
        }

