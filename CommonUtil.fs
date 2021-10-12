module Cutils

open System
open Akka
open Akka.FSharp
open FSharpx
let systemName = "SimGossip"
let actorName = "ActGossip";
let mutable gossipSystem = Unchecked.defaultof<Actor.ActorSystem>

type ActorWrapper = { Actor: Actor.IActorRef; mutable Converged: bool }

type Message = 
    | SendRumor
    | Rumor
    | GetNonConverged
    | Converged of string
    | RemoveNeighbor of (int*int*int)
    | CreateActors
    | InitActors
    | PushSumMessage of (float*float)


let setSystem system = 
    gossipSystem <- system


let findPerfectCube (num: int) =
    let mutable output = float num
    let mutable cubeFound = false

    while not(cubeFound) do
        let cubeRoot = Math.Pow(output, 1.0/3.0)
        let diff = cubeRoot - floor (cubeRoot + 0.00001) //Adding 0.00001 to avoid any rounding error for float
        if diff > float 0 then 
            output <- output + 1.0 
        else cubeFound <- true
    int (floor (Math.Pow(output, 1.0/3.0) + 0.00001)) //Adding to avoid any rounding error for float

// Checking the converged state
//**************************************************************

let fetchCurrentIndex actorName = 
    let indices: string[] = actorName |> String.splitChar [| '_' |]
    (indices.[1] |> int, indices.[2] |> int, indices.[3] |> int)


let testPushSumConvergeState (currentVals: double*double) (receivedValues: double*double) (debug: bool) = 
    let oldRatio = fst(currentVals)/snd(currentVals)
    let newRatio = ((fst(currentVals)+fst(receivedValues))/2.0)/((snd(currentVals)+snd(receivedValues))/2.0)
    Double.IsNaN (abs (oldRatio - newRatio)) || abs (oldRatio - newRatio) < 10.0 ** -10.0

// Fetching Random neibours in Full
//****************************************************************

let fetchNeighborIndicesFull cols =
    Array.init cols (fun y -> (0, 0, y))

// Fetching Random neibours in Line
//****************************************************************    

let fetchNeighborIndicesLine currentCol cols = 
    if currentCol = 0 then [| (0, 0, 1) |]
    elif currentCol = cols-1 then [| (0, 0, cols-2) |]
    else [| (0, 0, currentCol+1); (0, 0, currentCol-1) |]

// Fetching Random neibours in 3D
//****************************************************************

let fetchNeighborIndices3D layers rows cols currentLayer currentRow currentCol topology = 
    let neighbors: (int*int*int)[] = Array.init (if topology = "3D" then 6 else 7) (fun x -> (0, 0, 0))
    let mutable counter = 0
    let helper: int[] = [| 1; -1 |]
    let helperIndices: int[] = [| layers-1; rows-1; cols-1 |]
    let currentPos = [| currentLayer; currentRow; currentCol |]

    for i in 0..2 do
        for j in helper do
            currentPos.[i] <- currentPos.[i] + j

            if currentPos.[i] >=0 && currentPos.[i] <= helperIndices.[i] then
                neighbors.[counter] <- (currentPos.[0], currentPos.[1], currentPos.[2])
                counter <- counter + 1

    if topology = "imp3D" then
        let mutable randomIndex = (Random().Next(layers), Random().Next(rows), Random().Next(cols))
        while Array.contains randomIndex neighbors || randomIndex = (currentLayer, currentRow, currentRow) do
            randomIndex <- (Random().Next(layers), Random().Next(rows), Random().Next(cols))
        neighbors.[counter] <- randomIndex
        counter <- counter + 1

    neighbors.[0..counter-1]

// Mathcing the command line arg for the requested toplogy 
//*************************************************************************

let fetchNeighborIndices topology layers rows cols currentLayer currentRow currentCol = 
    match topology with
    | "full" -> 
        fetchNeighborIndicesFull cols
    | "line" ->
        fetchNeighborIndicesLine currentCol cols
    | "3D" | "imp3D" ->
        fetchNeighborIndices3D layers rows cols currentLayer currentRow currentCol topology
    | _ -> Unchecked.defaultof<(int*int*int)[]>


let fetchRandomNeighbor (actors: ActorWrapper[,,]) (neighbors: (int*int*int)[]) layer row col = 
    if neighbors.Length > 0 then
        let (z, x, y) = neighbors.[Random().Next(neighbors.Length)]
        actors.[z, x, y].Actor
    else
        actors.[layer, row, col].Actor