[<AutoOpen>]
module EasyHttp.Types

open System
open System.Net.Http

type ESerializationType =
    | Json        = 0
    | QueryString = 1
    | PathString  = 2

// little bit of extra type safety not using an enum where possible
type SerializationType =
    | JsonSerialization
    | QueryStringSerialization
    | PathStringSerialization

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
        | ESerializationType.PathString -> PathStringSerialization
        | _ -> invalidArg (nameof serializationType) $"'{serializationType}' is not a supported serialization type."

[<Obsolete("Exposed only to support makeApi being inline.")>]
type Endpoint =
    {
        /// The relative path to be used on top of the base uri
        Path: string
        /// The method the request should use
        Method: HttpMethod
        /// The serialization type the request should use
        SerializationType: SerializationType
        /// The signature of a provided function
        FunctionType: Type
        /// The argument of the aforementioned function
        ArgumentType: Type
        /// The return type of the aforementioned function
        ReturnType: Type
    }