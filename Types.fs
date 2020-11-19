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

[<AttributeUsage(AttributeTargets.Property)>]
type PathAttribute(path: string) =
    inherit Attribute()
    member __.Path = path

[<AttributeUsage(AttributeTargets.Property)>]
type MethodAttribute(method: string) =
    inherit Attribute()
    member __.Method = HttpMethod method

[<AttributeUsage(AttributeTargets.Property)>]
type SerializationOverrideAttribute(serializationType: ESerializationType) =
    inherit Attribute()
    member __.SerializationType =
        match serializationType with
        | ESerializationType.Json -> JsonSerialization
        | ESerializationType.QueryString -> QueryStringSerialization
        | _ -> invalidArg (nameof serializationType) $"'{serializationType}' is not a supported serialization type."

type Endpoint =
    {
        Path: string
        Method: HttpMethod
        SerializationType: SerializationType
        FunctionType: Type
        ArgumentType: Type
        ReturnType: Type
    }