[![NuGet Badge](https://buildstats.info/nuget/FsEasyHttp)](https://www.nuget.org/packages/FsEasyHttp/)

## What?
This package was/is designed to fill in a Remoting gap that the amazing project [Bolero](github.com/fsbolero/Bolero) has. That gap being that the remoting doesn't work unless it is defined on the server as well. This means that integrating Bolero into your server/client meant redefining or reworking your APIs. This project bridges that gap by functioning similarly to Bolero's Remote API definitions, but instead can hit any arbitrary HTTP-based API.

## Enough talk, let's see some documentation!
All of the public (and most of the private) API exposed by the package is available through XML documentation (viewable through intellisense). But, here's a quick example using my [echo server](https://github.com/ChernayaKoshka/EchoServer) as an endpoint (any server with a reachable API will do).

The following code is available in this repo [Test.fsx](./Test.fsx)

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
```

Let's break that down, shall we?:

1. All of the _inputs_ are either `unit` or an anonymous record in this example. Named records work as well, but the anonymous record syntax may be more convenient.
   * This is because (currently), the only accepted function definitions have a single input (a record or unit type) and a single output (any JSON serializable object). This choice was made in order to support JSON payloads and query string serialization.
2. The _name_ of a function is purely for the caller's benefit
3. `SerializationOverride` is an attribute that takes an enum with one of two values:
   1. `ESerializationType.Json`, this will serialize the function input to JSON
   2. `ESerializationType.QueryString`, this will serialize the function input to a query string (ie: `?key1=val1&key2=val2`). This method is inflexible and only supports primitive/option types.

   It should be noted that the default serialization method for HTTP methods that allow a body is JSON. Any that do not allow a body default to query string serialization.
   In addition, if a defined function's return type is `unit`, it will return `unit` without reading the body of the response.
4. `Method` is an attribute that defines the HTTP Verb to use when making a request. It should be noted that the default method is `POST`
5. `Path` is an attribute that defines any additional pathing to use on top of the `BaseUri` provided.

Notes:
* JSON serialization is not supported for verbs that do not allow a body. This is to stay compatible with WASM.
* Multiple different attributes are allowed on a single function. (ie, if you wanted to specify both a `Method` and a `Path` attribute)

Finally, all you have to do is call `makeApi<TestRecord> id` to create the record! Example follows:

```fs
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
```

Output:
```
TestQueryString result:
{ Method = "POST"
  Path = "/"
  QueryString = "?someNumber=1000"
  Content = "" }

Test result:
{ Method = "POST"
  Path = "/"
  QueryString = ""
  Content = "{"someNumber":1000}" }

TestDelete result:
{ Method = "DELETE"
  Path = "/"
  QueryString = ""
  Content = "" }

UnitFunction result:
()
```

### Additional Info
* JSON serialization configuration is not yet supported, but is a planned feature
* HttpClient customization can be performed in the only argument to `makeApi<'T>`, as its signature is `makeApi<'T>: (HttpClient -> HttpClient)`. If no configuration is needed, the F# function `id` can be passed in.

---

### Rambling on why I started this project
I didn't want to redefine my APIs around Bolero's remoting, so I had originally created a [branch](https://github.com/fsbolero/Bolero/compare/master...ChernayaKoshka:RemotingQueryStringSerializer) to allow for non-Bolero API definitions. However, as the project grew, I decided that the project wasn't relying on any Bolero functionality and could be extracted. So, that's what I've done here.