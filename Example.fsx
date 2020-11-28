#r "nuget:ply"
#load @"Types.fsi" @"Types.fs"
#load @"Serializers\Utils.fsi" @"Serializers\Utils.fs"
#load @"Serializers\PathString.fsi" @"Serializers\PathString.fs"
#load @"Serializers\QueryString.fsi" @"Serializers\QueryString.fs"
#load @"EasyHttp.fsi" @"EasyHttp.fs"

open System
open EasyHttp
open System.Net.Http
open System.Threading.Tasks

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
        TestJson: {| someNumber: int |} -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("/{!query!}")>]
        TestQueryString: {| someNumber: int |} -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{someData}/{someNumber}{!query!}")>]
        TestPathString: {| someData: string; someNumber: int; someQuery: string; someQuery2: string |} -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{someData}/{someNumber}{!query!}")>]
        TestOptionalQueryString: {| someData: string; someNumber: int; someQuery: string; someQuery2: string option |} -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{someData}/{someNumber}")>]
        TestOptionalPathString: {| someData: string; someNumber: int option |} -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("/some/endpoint/{!ordered!}")>]
        TestOrderedPathString: SomeOrderedData -> Task<Response>

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{!ordered!}")>]
        TestOrderedPathStringAnonRecord: {| ZData: string; AData: string |} -> Task<Response>

        [<Method("DELETE")>]
        TestDelete: unit -> Task<Response>

        StringResult: unit -> Task<string>

        [<Path("/some/other/endpoint")>]
        UnitFunction: unit -> Task<unit>
    }
    with
        static member BaseUri = Uri("http://localhost:8080")

let result =
    match makeApi<TestRecord> (new HttpClient()) with
    | Ok s -> s
    | Error err -> failwith err

let inline runPrint fmt t =
    t
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> printfn fmt

result.TestJson {| someNumber = 1000 |}
|> runPrint "Test result:\n%A\n"

result.TestQueryString {| someNumber = 1000 |}
|> runPrint"TestQueryString result:\n%A\n"

// [<Path("{someData}/{someNumber}{!query!}")>]
result.TestPathString {| someData = "blah"; someNumber = 32; someQuery = "queryParamValue1"; someQuery2 = "queryParamValue2" |}
|> runPrint "TestPathString result:\n%A\n"

// [<Path("{someData}/{someNumber}{!query!}")>]
result.TestOptionalQueryString {| someData = "blah"; someNumber = 32; someQuery = "queryParamValue1"; someQuery2 = None |}
|> runPrint"TestOptionalQueryString result:\n%A\n"

// [<Path("{someData}/{someNumber}")>]
try
    result.TestOptionalPathString {| someData = "blah"; someNumber = None |}
    |> runPrint "This succeeded?: %A"
with
| :? AggregateException as ae ->
    printfn "TestOptionalPathString result:\n%s\n" ae.InnerException.Message

// [<Path("/some/endpoint/{!ordered!}")>]
result.TestOrderedPathString { ZData = "Zee"; AData = "Cool data"; QData = "Quickly qooler data" }
|> runPrint "TestOrderedPathString result:\n%A\n"

// [<Path("{!ordered!}")>]
result.TestOrderedPathStringAnonRecord {| ZData = "First"; AData = "Second" |}
|> runPrint "TestOrderedPathStringAnonRecord result:\n%A\n"

result.TestDelete()
|> runPrint "TestDelete result:\n%A\n"

result.StringResult()
|> runPrint "StringResult result:\n%A\n"

result.UnitFunction()
|> runPrint "UnitFunction result:\n%A\n"