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
        | "get" | "delete" | "trace" | "options" | "head" -> true
        | _ -> false

let private checkMethodAndSerializationTypeCompatible method serializationType =
    serializationType = ESerializationType.Json && not (methodAllowsBody method)

let private getDefaultSerializationType (method: string) =
        if methodAllowsBody method then ESerializationType.Json
        else ESerializationType.QueryString

[<AttributeUsage(AttributeTargets.Property)>]
type EndpointDescriptionAttribute(method: string, serializationType: ESerializationType) =
    inherit Attribute()

    do if Enum.GetValues<ESerializationType>() |> Seq.contains serializationType |> not then
        invalidArg (nameof serializationType) $"'{serializationType}' is not a valid serialization type."

    new (serializationType: ESerializationType) =
        EndpointDescriptionAttribute((if serializationType = ESerializationType.Json then "POST" else "GET"), serializationType)
    new (method: string) =
        EndpointDescriptionAttribute(method, getDefaultSerializationType method)
    new (method: EHttpMethod) =
        let method = string method
        EndpointDescriptionAttribute(method, getDefaultSerializationType method)
    new (method: EHttpMethod, serializationType: ESerializationType) =
        EndpointDescriptionAttribute(string method, serializationType)

    member val Method = HttpMethod method
    member val SerializationType = serializationType
    member __.AreMethodAndSerializationTypeCompatible = checkMethodAndSerializationTypeCompatible method serializationType
    static member Default = EndpointDescriptionAttribute("POST", ESerializationType.Json)

[<AttributeUsage(AttributeTargets.Property)>]
type ApiOptionsAttribute(serializationType: ESerializationType, httpMethod: string) =
    inherit Attribute()
    member val SerializationType =
        match serializationType with
        | ESerializationType.Json -> JsonSerialization
        | ESerializationType.QueryString -> QueryStringSerialization
        | other -> failwith $"Unsupported ESerializationType {other}"

type EndPoint =
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