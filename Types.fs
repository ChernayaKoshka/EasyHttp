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
    member __.SerializationType = serializationType

type Endpoint =
    {
        Path: string
        Method: HttpMethod
        SerializationType: SerializationType
        ArgumentType: Type
        ReturnType: Type
    }

type IApiDefinition =
    abstract BaseUrl: string
    abstract DefaultSerializationType: SerializationType