[![NuGet Badge](https://buildstats.info/nuget/FsEasyHttp)](https://www.nuget.org/packages/FsEasyHttp/)

## What?
This package was/is designed to fill in a Remoting gap that the amazing project [Bolero](github.com/fsbolero/Bolero) has. That gap being that the remoting doesn't work unless it is defined on the server as well. This means that integrating Bolero into your server/client meant redefining or reworking your APIs. This project bridges that gap by functioning similarly to Bolero's Remote API definitions, but instead can hit any arbitrary HTTP-based API.

## Enough talk, let's see some documentation!
All of the public (and most of the private) API exposed by the package is available through XML documentation (viewable through intellisense). But, here's a quick example using my [echo server](https://github.com/ChernayaKoshka/EchoServer) as an endpoint (any server with a reachable API will do).

The following code in its entirety is available in this repo [Example.fsx](./Example.fsx)

First, I'm going to define the response type expected from the endpoint(s). This can be any JSON-serializable object. In this case, my echo server returns a record serialized to JSON like so:
```fs
type Response =
    {
        Method: string
        Path: string
        QueryString: string
        Content: string
    }
```

Second, we need to define the API we're going to hit. This takes form of a record containing nothing except for function definitions, a static BasicUri member, and some attributes.
```fs
type SomeOrderedData =
    {
        First: string
        Second: string
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
        [<Path("{someData}/{someNumber}{!query!}")>]
        TestOptionalQueryString: {| someData: string; someNumber: int; someQuery: string; someQuery2: string option |} -> Response

        [<SerializationOverride(ESerializationType.PathString)>]
        [<Path("{someData}/{someNumber}")>]
        TestOptionalPathString: {| someData: string; someNumber: int option |} -> Response

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
```

Let's break that down, shall we?:

1. All of the _inputs_ are either `unit` or an anonymous record in this example. Named records work as well, but the anonymous record syntax may be more convenient.
   * This is because (currently), the only accepted function definitions have a single input (a record or unit type) and a single output (any JSON serializable object). This choice was made in order to support JSON payloads and query string serialization.
2. The _name_ of a function is purely for the caller's benefit
3. `SerializationOverride` is an attribute that takes an enum with one of two values:
   1. `ESerializationType.Json`, this will serialize the function input to JSON
   2. `ESerializationType.PathString`, this will serialize the function input to a path string. This method is inflexible and only supports primitive/option types. For example, assuming a record of `{| Test = "SomeValue"; Blah = 32|}` and a `Path` attribute of `[<Path("{Test}{!query!}")>]` would result in the request hitting `SomeValue?Blah=32`. More detailed examples later on.

   It should be noted that the default serialization method for HTTP methods that allow a body is JSON. Any that do not allow a body default to path string serialization.
   In addition, if a defined function's return type is `unit`, it will return `unit` without reading the body of the response.
4. `Method` is an attribute that defines the HTTP Verb to use when making a request. It should be noted that the default method is `POST`
5. `Path` is an attribute that defines any additional pathing to use on top of the `BaseUri` provided. If the serialization type is `PathString`, it will also be populated with serialized values. Format/special markers follow:
   1. `{fieldName}` - Gets replaced with the corresponding record field value. Optional values _are not supported_ and will throw an exception.
   2. `{!ordered!}` - Simply concats the record's field's values between slashes in the order they are defined in the record. There's a [significant gotcha](#warning) with anonymous records.
   3. `{!query!}` - Serializes the _remaining_ record fields to a query string (e.g. `?key1=val1&key2=val2`) Optional (`Option<_>`) values are supported in this case. This special marker _must_ occur at the end of the path, or it will be ignored. In the event that this behavior is not desired, the `{fieldName}` syntax can be used instead. (e.g. `{myField}?myField={myField}` will result in `myFieldValue?myField=myFieldValue`)

   For example, given a record of `{ Blah = "Something"; CoolNumber = 42; ANumber = 2; AString = "some cool string" }` and a `Path` of `{Blah}/{CoolNumber}{!query!}` will result in a path of `Something/32?ANumber=2&AString=some+cool+string`.

Notes:
* JSON serialization is not supported for verbs that do not allow a body. This is to stay compatible with WASM.
* Multiple _different_ attributes are allowed on a single function. (i.e., if you wanted to specify both a `Method` and a `Path` attribute)

Finally, all you have to do is call `makeApi<TestRecord> id` to create the record! Example follows:

```fs
let result =
    match makeApi<TestRecord> id with
    | Ok s -> s
    | Error err -> failwith err

result.TestJson {| someNumber = 1000 |}
|> printfn "Test result:\n%A\n"

result.TestQueryString {| someNumber = 1000 |}
|> printfn "TestQueryString result:\n%A\n"

// [<Path("{someData}/{someNumber}{!query!}")>]
result.TestPathString {| someData = "blah"; someNumber = 32; someQuery = "queryParamValue1"; someQuery2 = "queryParamValue2" |}
|> printfn "TestPathString result:\n%A\n"

// [<Path("{someData}/{someNumber}{!query!}")>]
result.TestOptionalQueryString {| someData = "blah"; someNumber = 32; someQuery = "queryParamValue1"; someQuery2 = None |}
|> printfn "TestOptionalQueryString result:\n%A\n"

// [<Path("{someData}/{someNumber}")>]
try
    result.TestOptionalPathString {| someData = "blah"; someNumber = None |}
    |> ignore
with
| :? System.Reflection.TargetInvocationException as tie ->
    printfn "TestOptionalPathString result:\n%s\n" tie.InnerException.Message

// [<Path("/some/endpoint/{!ordered!}")>]
result.TestOrderedPathString { ZData = "Zee"; AData = "Cool data"; QData = "Quickly qooler data" }
|> printfn "TestOrderedPathString result:\n%A\n"

// [<Path("{!ordered!}")>]
result.TestOrderedPathStringAnonRecord {| ZData = "First"; AData = "Second" |}
|> printfn "TestOrderedPathStringAnonRecord result:\n%A\n"

result.TestDelete()
|> printfn "TestDelete result:\n%A\n"

result.UnitFunction()
|> printfn "UnitFunction result:\n%A\n"
```

Output:
```
Test result:
{ Method = "POST"
  Path = "/"
  QueryString = ""
  Content = "{"someNumber":1000}" }

TestQueryString result:
{ Method = "POST"
  Path = "/"
  QueryString = "?someNumber=1000"
  Content = "" }

TestPathString result:
{ Method = "POST"
  Path = "/blah/32"
  QueryString = "?someQuery=queryParamValue1&someQuery2=queryParamValue2"
  Content = "" }

TestOptionalQueryString result:
{ Method = "POST"
  Path = "/blah/32"
  QueryString = "?someQuery=queryParamValue1"
  Content = "" }

TestOptionalPathString result:
Empty path values are not supported. Offending fields follow: 'someNumber'

TestOrderedPathString result:
{ Method = "POST"
  Path = "/some/endpoint/Zee/Cool+data/Quickly+qooler+data"
  QueryString = ""
  Content = "" }

TestOrderedPathStringAnonRecord result:
{ Method = "POST"
  Path = "/Second/First"
  QueryString = ""
  Content = "" }

TestDelete result:
{ Method = "DELETE"
  Path = "/"
  QueryString = ""
  Content = "" }

UnitFunction result:
()
```

#### **Warning!**
In the above output, notice that the `TestOrderedPathStringAnonRecord` result _is not in the order we defined the anonymous record in!_ This is [intentional behavior](https://github.com/dotnet/fsharp/issues/6422#issuecomment-479504357) according to the developers of F# as anonymous records have their fields sorted by name.

### Additional Info
* JSON serialization configuration is not yet supported, but is a planned feature
* Mixed-mode serialization (path + JSON) is not supported, but may be supported in the future if it is desired (or I want it).
* HttpClient customization can be performed in the only argument to `makeApi<'T>`, as its signature is `makeApi<'T>: (HttpClient -> HttpClient)`. If no configuration is needed, the F# function `id` can be passed in.

---

### Rambling on why I started this project
I didn't want to redefine my APIs around Bolero's remoting, so I had originally created a [branch](https://github.com/fsbolero/Bolero/compare/master...ChernayaKoshka:RemotingQueryStringSerializer) to allow for non-Bolero API definitions. However, as the project grew, I decided that the project wasn't relying on any Bolero functionality and could be extracted. So, that's what I've done here.