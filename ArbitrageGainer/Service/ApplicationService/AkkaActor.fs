module Service.ApplicationService.AkkaActor

open Akka.Actor
open Akka.FSharp
open Service.ApplicationService.PnL
let system = ActorSystem.Create "PnLSystem"
let pnlActor = createPnLActor system

