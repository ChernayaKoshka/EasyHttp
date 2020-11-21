#load @"Types.fsi" @"Types.fs"
#load @"Serializers\Utils.fsi" @"Serializers\Utils.fs"
#load @"Serializers\PathString.fsi" @"Serializers\PathString.fs"
#load @"Serializers\QueryString.fsi" @"Serializers\QueryString.fs"
#load @"EasyHttp.fsi" @"EasyHttp.fs"

open System
open EasyHttp

type Response =
    {
        Method: string
        Path: string
        QueryString: string
        Content: string
    }

type SomeOrderedData =
    {
        ZData: string
        AData: string
        QData: string
    }

type TestRecord =
    {
        TestJson: {| someNumber: int |} -> Response

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("/{!query!}")>]
        TestQueryString: {| someNumber: int |} -> Response

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{someData}/{someNumber}{!query!}")>]
        TestPathString: {| someData: string; someNumber: int; someQuery: string; someQuery2: string |} -> Response

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("/some/endpoint/{!ordered!}")>]
        TestOrderedPathString: SomeOrderedData -> Response

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{!ordered!}")>]
        TestOrderedPathStringAnonRecord: {| ZData: string; AData: string |} -> Response

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

result'.TestJson {| someNumber = 1000 |}
|> printfn "Test result:\n%A\n"

result'.TestQueryString {| someNumber = 1000 |}
|> printfn "TestQueryString result:\n%A\n"

// [<Path("{someData}/{someNumber}{!query!}")>]
result'.TestPathString {| someData = "blah"; someNumber = 32; someQuery = "queryParamValue1"; someQuery2 = "queryParamValue2" |}
|> printfn "TestPathString result:\n%A\n"

// [<Path("/some/endpoint/{!ordered!}")>]
result'.TestOrderedPathString { ZData = "Zee"; AData = "Cool data"; QData = "Quickly qooler data" }
|> printfn "TestOrderedPathString result:\n%A\n"

// [<Path("{!ordered!}")>]
result'.TestOrderedPathStringAnonRecord {| ZData = "ZData"; AData = "AData" |}
|> printfn "TestOrderedPathStringAnonRecord result:\n%A\n"

result'.TestDelete()
|> printfn "TestDelete result:\n%A\n"

result'.UnitFunction()
|> printfn "UnitFunction result:\n%A\n"