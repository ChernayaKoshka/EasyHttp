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
    | PathString  = 2

/// <Summary>
/// Contains the supported serialization types.
/// </Summary>
type SerializationType =
    | JsonSerialization
    | QueryStringSerialization
    | PathStringSerialization

/// <Summary>
/// An attribute that when applied to a function, will contain an additional path fragment to use on top of the provided base path.
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type PathAttribute =
    new: path: string
        -> PathAttribute
    inherit Attribute
    member Path: string

/// <Summary>
/// Specifies the `HttpMethod` that should be used.
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type MethodAttribute =
    new: method: string
        -> MethodAttribute
    inherit Attribute
    member Method: HttpMethod

/// <Summary>
/// Overrides the default serialization type for a given `HttpMethod`
/// </Summary>
[<AttributeUsage(AttributeTargets.Property)>]
type SerializationOverrideAttribute =
    new: serializationType: ESerializationType
        -> SerializationOverrideAttribute
    inherit Attribute
    member SerializationType: SerializationType

/// <Summary>
/// Internally used to define an endpoint
/// </Summary>
type Endpoint =
    {
        Path: string
        Method: HttpMethod
        SerializationType: SerializationType
        FunctionType: Type
        ArgumentType: Type
        ReturnType: Type
    }