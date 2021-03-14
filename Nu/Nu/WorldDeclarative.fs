﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open Prime
open Nu

/// Describes the behavior of a screen.
type [<NoEquality; NoComparison>] ScreenBehavior =
    | Vanilla
    | Dissolve of DissolveDescriptor * SongDescriptor option
    | Splash of DissolveDescriptor * SplashDescriptor * SongDescriptor option * Screen
    | OmniScreen

/// Describes the content of an entity.
type [<NoEquality; NoComparison>] EntityContent =
    | EntitiesFromStream of Lens<obj, World> * (obj -> obj) * (obj -> World -> MapGeneralized) * (obj -> Lens<obj, World> -> World -> EntityContent)
    | EntityFromInitializers of string * string * PropertyInitializer list * EntityContent list
    | EntityFromFile of string * string
    interface SimulantContent

    /// Expand an entity content to its constituent parts.
    static member expand content (group : Group) (world : World) =
        match content with
        | EntitiesFromStream (lens, sieve, unfold, mapper) ->
            Choice1Of3 (lens, sieve, unfold, mapper)
        | EntityFromInitializers (dispatcherName, name, initializers, content) ->
            let (descriptor, handlersEntity, bindsEntity) = Describe.entity4 dispatcherName (Some name) initializers (group / name) world
            Choice2Of3 (name, descriptor, handlersEntity, bindsEntity, (group / name, content))
        | EntityFromFile (name, filePath) ->
            Choice3Of3 (name, filePath)

/// Describes the content of a group.
type [<NoEquality; NoComparison>] GroupContent =
    | GroupsFromStream of Lens<obj, World> * (obj -> obj) * (obj -> World -> MapGeneralized) * (obj -> Lens<obj, World> -> World -> GroupContent)
    | GroupFromInitializers of string * string * PropertyInitializer list * EntityContent list
    | GroupFromFile of string * string
    interface SimulantContent

    /// Expand a group content to its constituent parts.
    static member expand content screen (world : World) =
        match content with
        | GroupsFromStream (lens, sieve, unfold, mapper) ->
            Choice1Of3 (lens, sieve, unfold, mapper)
        | GroupFromInitializers (dispatcherName, name, initializers, content) ->
            let group = screen / name
            let expansions = List.map (fun content -> EntityContent.expand content group world) content
            let streams = List.map (function Choice1Of3 (lens, sieve, unfold, mapper) -> Some (group, lens, sieve, unfold, mapper) | _ -> None) expansions |> List.definitize
            let descriptors = List.map (function Choice2Of3 (_, descriptor, _, _, _) -> Some descriptor | _ -> None) expansions |> List.definitize
            let handlers = List.map (function Choice2Of3 (_, _, handlers, _, _) -> Some handlers | _ -> None) expansions |> List.definitize |> List.concat
            let binds = List.map (function Choice2Of3 (_, _, _, binds, _) -> Some binds | _ -> None) expansions |> List.definitize |> List.concat
            let entityContents = List.map (function Choice2Of3 (_, _, _, _, entityContents) -> Some entityContents | _ -> None) expansions |> List.definitize
            let filePaths = List.map (function Choice3Of3 filePath -> Some filePath | _ -> None) expansions |> List.definitize |> List.map (fun (entityName, path) -> (name, entityName, path))
            let (descriptor, handlersGroup, bindsGroup) = Describe.group5 dispatcherName (Some name) initializers descriptors group world
            Choice2Of3 (name, descriptor, handlers @ handlersGroup, binds @ bindsGroup, streams, filePaths, entityContents)
        | GroupFromFile (name, filePath) ->
            Choice3Of3 (name, filePath)

/// Describes the content of a screen.
type [<NoEquality; NoComparison>] ScreenContent =
    | ScreenFromInitializers of string * string * ScreenBehavior * PropertyInitializer list * GroupContent list
    | ScreenFromGroupFile of string * ScreenBehavior * Type * string
    | ScreenFromFile of string * ScreenBehavior * string
    interface SimulantContent

    /// Expand a screen content to its constituent parts.
    static member expand content (_ : Game) world =
        match content with
        | ScreenFromInitializers (dispatcherName, name, behavior, initializers, content) ->
            let screen = Screen name
            let expansions = List.map (fun content -> GroupContent.expand content screen world) content
            let streams = List.map (function Choice1Of3 (lens, sieve, unfold, mapper) -> Some (screen, lens, sieve, unfold, mapper) | _ -> None) expansions |> List.definitize
            let descriptors = List.map (function Choice2Of3 (_, descriptor, _, _, _, _, _) -> Some descriptor | _ -> None) expansions |> List.definitize
            let handlers = List.map (function Choice2Of3 (_, _, handlers, _, _, _, _) -> Some handlers | _ -> None) expansions |> List.definitize |> List.concat
            let binds = List.map (function Choice2Of3 (_, _, _, binds, _, _, _) -> Some binds | _ -> None) expansions |> List.definitize |> List.concat
            let entityStreams = List.map (function Choice2Of3 (_, _, _, _, stream, _, _) -> Some stream | _ -> None) expansions |> List.definitize |> List.concat
            let entityFilePaths = List.map (function Choice2Of3 (_, _, _, _, _, filePaths, _) -> Some (List.map (fun (groupName, entityName, filePath) -> (name, groupName, entityName, filePath)) filePaths) | _ -> None) expansions |> List.definitize |> List.concat
            let entityContents = List.map (function Choice2Of3 (_, _, _, _, _, _, entityContents) -> Some entityContents | _ -> None) expansions |> List.definitize |> List.concat
            let groupFilePaths = List.map (function Choice3Of3 (groupName, filePath) -> Some (name, groupName, filePath) | _ -> None) expansions |> List.definitize
            let (descriptor, handlersScreen, bindsScreen) = Describe.screen5 dispatcherName (Some name) initializers descriptors screen world
            Left (name, descriptor, handlers @ handlersScreen, binds @ bindsScreen, behavior, streams, entityStreams, groupFilePaths, entityFilePaths, entityContents)
        | ScreenFromGroupFile (name, behavior, ty, filePath) -> Right (name, behavior, Some ty, filePath)
        | ScreenFromFile (name, behavior, filePath) -> Right (name, behavior, None, filePath)

/// Describes the content of a game.
type [<NoEquality; NoComparison>] GameContent =
    | GameFromInitializers of string * PropertyInitializer list * ScreenContent list
    | GameFromFile of string
    interface SimulantContent

    /// Expand a game content to its constituent parts.
    static member expand content world =
        match content with
        | GameFromInitializers (dispatcherName, initializers, content) ->
            let game = Game ()
            let expansions = List.map (fun content -> ScreenContent.expand content game world) content
            let descriptors = Either.getLeftValues expansions |> List.map (fun (_, descriptor, _, _, _, _, _, _, _, _) -> descriptor)
            let handlers = Either.getLeftValues expansions |> List.map (fun (_, _, handlers, _, _, _, _, _, _, _) -> handlers) |> List.concat
            let binds = Either.getLeftValues expansions |> List.map (fun (_, _, _, binds, _, _, _, _, _, _) -> binds) |> List.concat
            let groupStreams = Either.getLeftValues expansions |> List.map (fun (_, _, _, _, _, stream, _, _, _, _) -> stream) |> List.concat
            let entityStreams = Either.getLeftValues expansions |> List.map (fun (_, _, _, _, _, _, stream, _, _, _) -> stream) |> List.concat
            let screenBehaviors = Either.getLeftValues expansions |> List.map (fun (screenName, _, _,  _, _, behavior, _, _, _, _) -> (screenName, behavior)) |> Map.ofList
            let groupFilePaths = Either.getLeftValues expansions |> List.map (fun (_, _, _, _, _, _, _, groupFilePaths, _, _) -> groupFilePaths) |> List.concat
            let entityFilePaths = Either.getLeftValues expansions |> List.map (fun (_, _, _, _, _, _, _, _, entityFilePaths, _) -> entityFilePaths) |> List.concat
            let entityContents = Either.getLeftValues expansions |> List.map (fun (_, _, _, _, _, _, _, _, _, entityContents) -> entityContents) |> List.concat
            let screenFilePaths = Either.getRightValues expansions
            let (descriptor, handlersGame, bindsGame) = Describe.game5 dispatcherName initializers descriptors game world
            Left (descriptor, handlers @ handlersGame, binds @ bindsGame, screenBehaviors, groupStreams, entityStreams, screenFilePaths, groupFilePaths, entityFilePaths, entityContents)
        | GameFromFile filePath -> Right filePath

/// Opens up some functions to make simulant lenses more accessible.
module Declarative =

    let Game = Game.Lens
    let Screen = Screen.Lens
    let Group = Group.Lens
    let Entity = Entity.Lens

[<AutoOpen>]
module DeclarativeOperators =

    /// Make a lens.
    let lens<'a> name get set this =
        Lens.make<'a, World> name get set this

    /// Make a read-only lens.
    let lensReadOnly<'a> name get this =
        Lens.makeReadOnly<'a, World> name get this

    /// Initialize a property.
    let set lens value =
        PropertyDefinition (define lens value)

    /// Initialize a property.
    let inline (==) left right =
        set left right

    /// Bind the left property to the value of the right.
    /// HACK: bind3 allows the use of fake lenses in declarative usage.
    /// NOTE: the downside to using fake lenses is that composed fake lenses do not function.
    let bind3 (left : Lens<'a, World>) (right : Lens<'a, World>) =
        if right.This :> obj |> isNull
        then failwith "bind3 expects an authentic right lens (where its This field is not null)."
        else BindDefinition (left, right)

    /// Bind the left property to the value of the right.
    let inline (<==) left right =
        bind3 left right

[<AutoOpen>]
module WorldDeclarative =

    type World with

        /// Turn a lens into a series of live simulants.
        /// OPTIMIZATION: lots of optimizations going on in here including inlining and mutation!
        static member expandSimulants
            (lens : Lens<obj, World>)
            (sieve : obj -> obj)
            (unfold : obj -> World -> MapGeneralized)
            (mapper : IComparable -> Lens<obj, World> -> World -> SimulantContent)
            (origin : ContentOrigin)
            (owner : Simulant)
            (parent : Simulant)
            world =
            let lensKey = Gen.id // a unique key for each lens given to the user
            let contentKey = match lens.PayloadOpt with Some (:? Guid as key) -> key | _ -> lensKey // a unique key for each content binding
            let lensGeneralized =
                let mutable lensResult = Unchecked.defaultof<obj>
                let mutable sieveResultOpt = None
                let mutable unfoldResultOpt = None
                { Lens.mapWorld (fun a world ->
                    let (b, c) =
                        if a === lensResult then
                            match (sieveResultOpt, unfoldResultOpt) with
                            | (Some sieveResult, Some unfoldResult) -> (sieveResult, unfoldResult)
                            | (Some sieveResult, None) -> (sieveResult, unfold sieveResult world)
                            | (None, Some _) -> failwithumf ()
                            | (None, None) -> let b = sieve a in (b, unfold b world)
                        else
                            match (sieveResultOpt, unfoldResultOpt) with
                            | (Some sieveResult, Some unfoldResult) -> let b = sieve a in if b === sieveResult then (b, unfoldResult) else (b, unfold b world)
                            | (Some _, None) -> let b = sieve a in (b, unfold b world)
                            | (None, Some _) -> failwithumf ()
                            | (None, None) -> let b = sieve a in (b, unfold b world)
                    lensResult <- a
                    sieveResultOpt <- Some b
                    unfoldResultOpt <- Some c
                    c)
                    lens
                    with PayloadOpt = Some (box lensKey) }
            let lensBinding = { CBMapper = mapper; CBSource = lensGeneralized; CBOrigin = origin; CBOwner = owner; CBParent = parent; CBSetKey = contentKey }
            let lensAddress = PropertyAddress.make lensGeneralized.Name lensGeneralized.This
            let world =
                { world with
                    ElmishBindingsMap =
                        match world.ElmishBindingsMap.TryGetValue lensAddress with
                        | (true, elmishBindings) ->
                            let elmishBindings = UMap.add lensKey (ContentBinding lensBinding) elmishBindings
                            UMap.add lensAddress elmishBindings world.ElmishBindingsMap
                        | (false, _) ->
                            let elmishBindings = if World.getStandAlone world then UMap.makeEmpty Imperative else UMap.makeEmpty Functional
                            let elmishBindings = UMap.add lensKey (ContentBinding lensBinding) elmishBindings
                            UMap.add lensAddress elmishBindings world.ElmishBindingsMap }
            //let world =
            //    if Lens.validate lensGeneralized world
            //    then World.publishBindingChange lensGeneralized.Name lensGeneralized.This world // expand simulants immediately rather than waiting for parent registration
            //    else world
            let world =
                World.monitor
                    (fun _ world ->
                        let world =
                            { world with
                                ElmishBindingsMap =
                                    match world.ElmishBindingsMap.TryGetValue lensAddress with
                                    | (true, elmishBindings) -> 
                                        let elmishBindings = UMap.remove lensKey elmishBindings
                                        if UMap.isEmpty elmishBindings
                                        then UMap.remove lensAddress world.ElmishBindingsMap
                                        else UMap.add lensAddress elmishBindings world.ElmishBindingsMap
                                    | (false, _) -> world.ElmishBindingsMap }
                        (Cascade, world))
                    (Events.Unregistering --> owner.SimulantAddress) // TODO: make sure that owner unregistration is the correct trigger!
                    owner // TODO: make sure this is the correct monitor target as well!
                    world
            world