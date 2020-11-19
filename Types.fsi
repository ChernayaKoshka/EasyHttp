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
type PathAttribute =
    new: string -> PathAttribute
    inherit Attribute
    member Path: string

[<AttributeUsage(AttributeTargets.Property)>]
type MethodAttribute =
    new: string -> MethodAttribute
    inherit Attribute
    member Method: HttpMethod

[<AttributeUsage(AttributeTargets.Property)>]
type SerializationOverrideAttribute =
    new: ESerializationType -> SerializationOverrideAttribute
    inherit Attribute
    member SerializationType: SerializationType

type Endpoint =
    {
        Path: string
        Method: HttpMethod
        SerializationType: SerializationType
        FunctionType: Type
        ArgumentType: Type
        ReturnType: Type
    }