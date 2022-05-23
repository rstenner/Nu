﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System

/// IO artifacts passively produced and consumed by Nu.
type [<NoEquality; NoComparison>] View =
    | Render2d of single * single * obj AssetTag * RenderDescriptor
    | PlaySound of single * Sound AssetTag
    | PlaySong of int * int * single * double * Song AssetTag
    | FadeOutSong of int
    | StopSong
    | SpawnEmitter of string * Particles.BasicEmitterDescriptor
    | Tag of string * obj
    | Views of View array
    | SegmentedViews of View SegmentedArray

[<RequireQualifiedAccess>]
module View =

    /// The empty view.
    let empty = Views [||]
