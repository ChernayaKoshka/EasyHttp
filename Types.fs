/// <Summary>
/// The types used throughout EasyHttp
/// </Summary>
[<AutoOpen>]
module EasyHttp.Types

open System
open System.Net.Http

/// <Summary>
/// Contains the supported serialization types as an enum for use as an attribute argument.
/// Otherwise, `SerializationType` should be used.
/// </Summary>
type ESerializationType =
    | Json        = 0
    | QueryString = 1

// little bit of extra type safety not using an enum where possible
/// <Summary>
/// Contains the supported serialization types.
/// </Summary>
type SerializationType =
    | JsonSerialization
    | QueryStringSerialization

/// <Summary>
/// An attribute that when applied to a function, will contain an additional path fragment to use on top of the provided base path.
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type PathAttribute(path: string) =
    inherit Attribute()
    member __.Path = path

/// <Summary>
/// Specifies the `HttpMethod` that should be used.
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type MethodAttribute(method: string) =
    inherit Attribute()
    member __.Method = HttpMethod method

/// <Summary>
/// Overrides the default serialization type for a given `HttpMethod`
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type SerializationOverrideAttribute(serializationType: ESerializationType) =
    inherit Attribute()
    member __.SerializationType =
        match serializationType with
        | ESerializationType.Json -> JsonSerialization
        | ESerializationType.QueryString -> QueryStringSerialization
        | _ -> invalidArg (nameof serializationType) $"'{serializationType}' is not a supported serialization type."

/// <Summary>
/// Internally used to define an endpoint
/// </Summary>
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