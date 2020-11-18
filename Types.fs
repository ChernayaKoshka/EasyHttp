[<AutoOpen>]
module EasyHttp.Types

open System
open System.Net.Http

type ESerializationType =
    | Json        = 0
    | QueryString = 1

// little bit of extra type safety not using an enum where possible
type SerializationType =
    | JsonSerialization
    | QueryStringSerialization

type EHttpMethod =
    | Get     = 0
    | Head    = 1
    | Post    = 2
    | Put     = 3
    | Delete  = 4
    | Connect = 5
    | Options = 6
    | Trace   = 7
    | Patch   = 8

let private methodAllowsBody (method: string) =
        match method.ToLowerInvariant() with
        | "get" | "delete" | "trace" | "options" | "head" -> false
        | _ -> true

let private checkMethodAndSerializationTypeCompatible method serializationType =
    serializationType = ESerializationType.Json && methodAllowsBody method

let private getDefaultSerializationType (method: string) =
        if methodAllowsBody method then ESerializationType.Json
        else ESerializationType.QueryString

[<AttributeUsage(AttributeTargets.Property)>]
type EndpointDescriptionAttribute(?path: string, ?method: string, ?serializationType: ESerializationType) =
    inherit Attribute()

    do
        match serializationType with
        | Some stype when Enum.GetValues<ESerializationType>() |> Seq.contains stype |> not ->
            invalidArg (nameof serializationType) $"'{serializationType}' is not a supported serialization type."
        | _ -> ()

    let path = Option.defaultValue "/" path
    let method = Option.defaultValue "POST" method
    let serializationType =
        match serializationType with
        | Some stype -> stype
        | None -> getDefaultSerializationType method

    do if checkMethodAndSerializationTypeCompatible method serializationType then ()
       else failwith $"{method} is not compatible with serialization type {serializationType}. Likely because it cannot have a body."

    member val Path: string = path
    member val Method = HttpMethod method
    member val SerializationType = serializationType
    member __.AreMethodAndSerializationTypeCompatible = checkMethodAndSerializationTypeCompatible method serializationType
    static member Default = EndpointDescriptionAttribute()

type Endpoint =
    {
        Path: string
        Method: HttpMethod
        SerializationType: SerializationType
        ArgumentType: Type
        ResultType: Type
    }

type IApiDefinition =
    abstract BaseUrl: string
    abstract DefaultSerializationType: SerializationType