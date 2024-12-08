module Service.ApplicationService.ActorAgent

open Akka.Actor
open Akka.FSharp
open Service.ApplicationService.PnL

// Shared ActorSystem
let actorSystem = ActorSystem.Create "PnLSystem"

// Shared PnLActor
let pnlActor = createPnLActor actorSystem


