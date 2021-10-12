module GossipSimulator

open System
open Akka
open Akka.FSharp

open Cutils

[<EntryPoint>]
let main argv = 
    let mutable nodeCount = (argv.[0] |> int)
    let topology = argv.[1];
    let algorithm = argv.[2];
    let mutable debug = false
        
    let nodePerfectCube = findPerfectCube nodeCount
    let (layer, row, col) = if topology = "full" || topology = "line" then (1, 1, int (float nodePerfectCube ** 3.0)) else (nodePerfectCube, nodePerfectCube, nodePerfectCube)
    nodeCount <- layer * row * col

    printfn "The count of the Nodes is %d" nodeCount

    let systemName = "GossipSimulator"
    let actorName = "GossipActor_";
    let mutable actorsGrid = Unchecked.defaultof<ActorWrapper[,,]>
    let mutable counter = 0
    let stopwatch = System.Diagnostics.Stopwatch()

    let gossipSystem = System.create systemName <| Configuration.defaultConfig()
    setSystem gossipSystem

// Defining the  gossip Actor 
//****************************************

    let gossipActor (neighbors: (int*int*int)[]) (mailbox: Actor<Message>) = 
        let mutable rumorsCount = 0
        let mutable nonConvergedNeighbors: (int*int*int)[] = neighbors

        let rec loop() = actor {
            let! message = mailbox.Receive()

            if not(stopwatch.IsRunning) then stopwatch.Start()

            let (currentLayer, currentRow, currentCol) = fetchCurrentIndex mailbox.Self.Path.Name
            let actorWrapper = actorsGrid.[currentLayer, currentRow, currentCol]

            if not (actorWrapper.Converged) then 
                match message with
                | SendRumor ->
                    fetchRandomNeighbor actorsGrid nonConvergedNeighbors currentLayer currentRow currentCol <! Rumor
                | Rumor ->
                    rumorsCount <- rumorsCount + 1
                    if (rumorsCount = 50) then
                        select ("akka://" + systemName + "/user/convergenceWatcher") gossipSystem <! Converged(mailbox.Self.Path.ToStringWithAddress())
                        actorWrapper.Converged <- true
                | RemoveNeighbor (z, x, y) ->
                    nonConvergedNeighbors <- Array.filter ((<>)(z, x, y)) nonConvergedNeighbors
                | _ -> "Invalid message received" |> ignore

            return! loop();
        }
        loop()

// Defining The push Sum Actor
//**********************************************************************************

    let pushSumActor (value: float) (neighbors: (int*int*int)[]) (mailbox: Actor<Message>) =
        let mutable s: double = value
        let mutable w: double = 1.0
        let mutable convergedCount: int = 0
        let mutable nonConvergedNeighbors = neighbors

        let rec loop() = actor {
            let! message = mailbox.Receive()

            if not(stopwatch.IsRunning) then stopwatch.Start()
            
            let (currentLayer, currentRow, currentCol) = fetchCurrentIndex mailbox.Self.Path.Name
            let currentActorWrapper = actorsGrid.[currentLayer, currentRow, currentCol]

            if not(currentActorWrapper.Converged) then
                match message with
                | SendRumor ->
                    s <- s/2.0
                    w <- w/2.0
                    fetchRandomNeighbor actorsGrid nonConvergedNeighbors currentLayer currentRow currentCol <! PushSumMessage(s, w)
                | PushSumMessage (newS, newW) ->
                    if testPushSumConvergeState (s, w) (newS, newW) debug then
                        convergedCount <- convergedCount + 1
                    else
                        convergedCount <- 0

                    if convergedCount = 3 then
                        currentActorWrapper.Converged <- true
                        select ("akka://" + systemName + "/user/convergenceWatcher") gossipSystem <! Converged(mailbox.Self.Path.Name)
                    else
                        s <- s + newS
                        w <- w + newW
                | RemoveNeighbor (z, x, y) ->
                    let length = nonConvergedNeighbors.Length
                    nonConvergedNeighbors <- Array.filter ((<>)(z, x, y)) nonConvergedNeighbors
                | _ -> "The message is Invalid" |> ignore

            return! loop()
        }
        loop()
//Definining The Gossip Boss 
//**********************************************************************************

    let gossipSupervisor =
        spawn gossipSystem "convergenceWatcher" (fun (mailbox: Actor<Message>) -> 
            let rec loop(convergedActorsCount: int) = actor {
                if (convergedActorsCount = nodeCount) then
                    stopwatch.Stop()
                    printfn "Program Complete!!!!!!!!!!!!!!"
                    printfn "All the actors were convereged Succeffully"
                    printfn "The total time taken for the convergence was -->>> %dms" stopwatch.ElapsedMilliseconds
                
                let! message = mailbox.Receive();
                let mutable newCount = convergedActorsCount

// To display the the convergence of one actor with the other with their IDs

                match message with
                | Converged actorId ->
                    printfn "The actor with ID ->>>>> %s converged ->>>>>>>> with %d" actorId convergedActorsCount
                    let (z, x, y) = fetchCurrentIndex actorId
                    for actor in mailbox.Context.GetChildren() do actor <! RemoveNeighbor((z, x, y))
                    newCount <- newCount + 1
                | CreateActors ->
                    match algorithm with
                    | "gossip" ->
                        actorsGrid <- Array3D.init layer row col (fun z x y ->
                            let neighbors = fetchNeighborIndices topology layer row col z x y 
                            {Actor=spawn mailbox.Context (actorName + z.ToString() + "_" + x.ToString() + "_" + y.ToString()) (gossipActor neighbors); Converged=false}
                        )
                    | "pushSum" ->
                        actorsGrid <- Array3D.init layer row col (fun z x y ->
                            counter <- counter+1
                            let neighbors = fetchNeighborIndices topology layer row col z x y
                            {Actor=spawn mailbox.Context (actorName + z.ToString() + "_" + x.ToString() + "_" + y.ToString()) (pushSumActor (float counter) neighbors); Converged=false}
                        )
                    | _ -> 0 |> ignore
                    mailbox.Self <! InitActors
                | InitActors ->
                    for i in 0..layer-1 do
                        for j in 0..row-1 do
                            for k in 0..col-1 do
                                gossipSystem.Scheduler.ScheduleTellRepeatedly(
                                    System.TimeSpan.FromMilliseconds(1000.0), System.TimeSpan.FromMilliseconds(50.0), actorsGrid.[i, j, k].Actor, SendRumor, actorsGrid.[i, j, k].Actor
                                )
                | _ -> printfn "Select a Valid function"
                return! loop(newCount)
            }
            loop (0))

    gossipSupervisor <! CreateActors

    Console.ReadLine() |> ignore

    0// return value for main function