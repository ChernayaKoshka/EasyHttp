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
        TestQueryString: {| someNumber: int |} -> Response

        [<SerializationOverride(serializationType = ESerializationType.Json)>]
        Test: {| someNumber: int |} -> Response

        [<Method("DELETE")>]
        TestDelete: unit -> Response

        [<Path("/some/other/endpoint")>]
        UnitFunction: unit -> unit
    }
    with
        static member BaseUri = Uri("http://localhost:8080")

let result = makeApi<TestRecord> id
let result' =
    match result with
    | Ok s -> s
    | Error err -> failwith err

result'.TestQueryString {| someNumber = 1000 |}
|> printfn "TestQueryString result:\n%A"

result'.Test {| someNumber = 1000 |}
|> printfn "Test result:\n%A"

result'.TestDelete()
|> printfn "TestDelete result:\n%A"

result'.UnitFunction()
|> printfn "UnitFunction result:\n%A"