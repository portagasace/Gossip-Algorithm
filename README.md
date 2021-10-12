# Gossip-Algorithm

The goal of this project is to determinethe convergence of such algorithms through a simulator based on actors writtenin F#.  
Since actors in F# are fully asynchronous, the particular type of Gossipimplemented is the so calledAsynchronous Gossip.

Gossip  Algorithm  for  information  propagationThe  Gossip  algorithminvolves the following:•Starting:A participant(actor) it told/sent a roumor(fact) by the mainprocess
•Step:Each actor selects a randomneighborand tells it the rumor
•Termination:Each actor keeps track of rumors and how many times ithas heard the rumor.  It stops transmitting once it has heard the rumor10 times (10 is arbitrary, you can select other values).
Push-Sum algorithm for sum computation
•State:Each actorAimaintains two quantities:sandw.  Initially,s=xi=i(that is actor numberihas valuei, play with other distribution ifyou so desire) andw= 1
•Starting:Ask one of the actors to start from the main process.•Receive:Messages sent and received are pairs of the form (s, w).  
Uponreceive,  an actor should add received pair to its own corresponding val-ues.  Upon receive, each actor selects a random neighboor and sends it amessage.
•Send:When sending a message to another actor, half ofsandwis keptby the sending actor and half is placed in the message.•Sum  estimate:At  any  given  moment  of  time,  the  sum  estimate  isswwheresandware the current values of an actor.
•Termination:If an actors ratioswdid not change more than 10−10in3 consecutive rounds the actor terminates.
WARNING: the valuessandwindependently never converge, only the ratio does.
