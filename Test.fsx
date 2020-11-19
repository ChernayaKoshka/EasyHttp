#load @"..\EasyHttp\Types.fs"
#load @"..\EasyHttp\QueryStringSerializer.fs"
#load @"..\EasyHttp\EasyHttp.fs"

open System
open EasyHttp

type Response =
    {
        Method: string
        Path: string
        QueryString: string
        Content: string
    }

type TestRecord =
    {
        [<SerializationOverride(ESerializationType.QueryString)>]
        Test: {| someNumber: int |} -> Response

        [<SerializationOverride(serializationType = ESerializationType.Json)>]
        TestQueryString: {| someNumber: int |} -> Response

        [<Method("DELETE")>]
        TestDelete: unit -> Response

        [<Path("/some/other/endpoint")>]
        UnitFunction: unit -> unit
    }

let result = getApiDefinition<TestRecord>(Uri("http://localhost:8080"), id)
let result' =
    match result with
    | Ok s -> s
    | Error err -> failwith err
result'.UnitFunction()