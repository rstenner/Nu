﻿namespace BlazeVector
open System
open Prime
open Nu

// this is a plugin for the Nu game engine that directs the execution of your application and editor
type BlazeVectorPlugin () =
    inherit NuPlugin ()

    // this exposes different editing modes in the editor
    override this.EditModes =
        Map.ofSeq
            [("Splash", fun world -> Game.SetModel Splash world)
             ("Title", fun world -> Game.SetModel Title world)
             ("Credits", fun world -> Game.SetModel Credits world)
             ("Gameplay", fun world -> Game.SetModel (Gameplay { State = Playing; Score = 0 }) world)]