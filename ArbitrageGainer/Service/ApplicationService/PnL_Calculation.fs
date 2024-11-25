module Service.ApplicationService.PnL
open System
open Core.Model.Models
open  Service.ApplicationService.Toggle

type PnLState = {
    TotalPnL: decimal
    PnLThreshold: decimal option
    Email: string option
}

// Initial PnL State
let initialPnLState = {
    TotalPnL = 0M
    PnLThreshold = None
    Email = None 
}

type PnLMessage =
    | AddTransaction of Transaction
    | SetThreshold of decimal
    | ResetThreshold
    | ResetAgent
    | RequestHistoricalPnL of DateTime * DateTime * AsyncReplyChannel<decimal list>
    | GetCurrentPnL of AsyncReplyChannel<decimal>

// calculate the PL of an order
let calculatePLofTransaction (order: Transaction) =
    match order.Side with
    | Sell -> order.Amount * order.Price
    | Buy -> -order.Amount * order.Price

// calculate the cumulated PL of a list of order
let calulatePLofTransactions (orders: List<Transaction>) =
    orders |> List.sumBy(calculatePLofTransaction)
 
// send email   
let sendEmail (address:string) (subject:string) (body:string) =
    printfn "Sending email to %s: %s - %s" address subject body

let PnLAgent = MailboxProcessor<PnLMessage>.Start(fun inbox ->
    let rec loop state =
        async{
            let! message = inbox.Receive()
            match message with
            | AddTransaction transaction ->
                let pl = calculatePLofTransaction transaction
                let newTotalPnL = state.TotalPnL + pl
                match state.PnLThreshold with
                | Some threshold when newTotalPnL >= threshold ->
                    match state.Email with
                    | Some email -> sendEmail email "Threshold Reached" (sprintf "Your PnL has reached %.2f." newTotalPnL)
                    | _ -> ()
                    let! toggleResult = toggleTrading ()
                    return! loop initialPnLState
                | _ -> return! loop {state with TotalPnL = newTotalPnL}
               
            | SetThreshold threshold ->
                let newThreshold =
                    match threshold with
                    | 0m -> None
                    | _ -> Some threshold
                return! loop{state with PnLThreshold = newThreshold }
            
            | GetCurrentPnL reply ->
                reply.Reply(state.TotalPnL)
                return! loop state  
                
            | ResetThreshold ->
                return! loop { state with PnLThreshold = None }
            
            | ResetAgent ->
                return! loop initialPnLState
            
            | _ -> return! loop state 
        }
    loop initialPnLState
)

// APIs of PnL Agent

// Record the Pnl
let addTransaction (transaction: Transaction) =
    PnLAgent.Post(AddTransaction transaction)

// Get Current PnL
let getCurrentPnL =
    PnLAgent.PostAndAsyncReply(GetCurrentPnL)

// Set a new PnL threshold
let setThreshold (threshold: decimal) =
    PnLAgent.Post(SetThreshold threshold)

// Reset the PnL Agent
let resetPnLAgent =
    PnLAgent.Post(ResetAgent)

