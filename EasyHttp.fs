[<AutoOpen>]
module EasyHttp.EasyHttp

open System
open System.IO
open System.Net
open System.Text
open System.Text.Json
open FSharp.Reflection
open System.Reflection
open System.Net.Http

let getAttributeContentsOrDefault (info: PropertyInfo) accessor defaultValue=
    let attribute = Attribute.GetCustomAttribute(info, typeof< 'b >)
    if isNull attribute then
        defaultValue
    else
        attribute
        :?> 'b
        |> accessor

let methodAllowsBody (method: HttpMethod) =
    match method.Method.ToLowerInvariant() with
    | "get" | "delete" | "trace" | "options" | "head" -> false
    | _ -> true

let extractEndpoints (t: Type) =
    t
    |> FSharpType.GetRecordFields
    |> Array.fold (fun (endpoints: Endpoint list, errors: string list) f ->

        if f.PropertyType |> FSharpType.IsFunction |> not then
            (endpoints, $"'{f.PropertyType.AssemblyQualifiedName}' is not an F# function." :: errors)
        else
        let argType, returnType = FSharpType.GetFunctionElements(f.PropertyType)

        if argType <> typeof<unit> && argType |> FSharpType.IsRecord |> not then
            (endpoints, $"The argument of {f.Name} must be an F# record or unit." :: errors)
        else
        let path = getAttributeContentsOrDefault f (fun (pa: PathAttribute) -> pa.Path) String.Empty
        let method = getAttributeContentsOrDefault f (fun (ma: MethodAttribute)-> ma.Method) HttpMethod.Post
        let serializationType =
            let defaultSerialization =
                if methodAllowsBody method then JsonSerialization
                else QueryStringSerialization
            getAttributeContentsOrDefault f (fun (soa: SerializationOverrideAttribute) -> soa.SerializationType) defaultSerialization

        if not (serializationType = JsonSerialization && (methodAllowsBody >> not) method) then
            (endpoints, $"{f.Name}: {method} and {serializationType} are not compatible. Likely because '{method}' does not allow a body." :: errors)
        else
        {
            Path = path
            Method = method
            SerializationType = serializationType
            FunctionType = f.PropertyType
            ArgumentType = argType
            ReturnType = returnType
        } :: endpoints, errors
    ) (List.empty, List.empty)
    |> fun (a, b) -> (List.rev a, List.rev b)

type private Http private () =
    static member Send<'ReturnType> (client: HttpClient) (method: HttpMethod) (serializationType: SerializationType) (requestUri: Uri) (content: obj) =
        printfn "invoked"
        let response =
            match serializationType with
            | JsonSerialization ->
                // TODO: Allow JsonSerializer to house serialization options? How would that work with an Attribute?
                let content = JsonSerializer.Serialize(content)
                new HttpRequestMessage(method, requestUri,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                )
            | QueryStringSerialization ->
                let queryString =
                    match QueryStringSerializer.serialize content with
                    | Ok queryString -> queryString
                    | Error error -> failwith error
                new HttpRequestMessage(method, UriBuilder(requestUri, Query = queryString).Uri)
            |> client.Send

        if typeof<'ReturnType> = typeof<unit> then
            box () :?> 'ReturnType
        else
            use reader = new StreamReader(response.Content.ReadAsStream())
            let data = reader.ReadToEnd()
            printfn "%A" data
            JsonSerializer.Deserialize<'ReturnType>(data)

let sendMethodInfo = typeof<Http>.GetMethod(nameof Http.Send, BindingFlags.NonPublic ||| BindingFlags.Static)

let makeApi<'Definition>(host: Uri) (configureClient: HttpClient -> HttpClient) =
    let t = typeof<'Definition>
    if t |> FSharpType.IsRecord |> not then
        Error $"{t.AssemblyQualifiedName} must be a record."
    else
    let endpoints, errors = extractEndpoints t
    if errors.Length <> 0 then
        errors
        |> String.concat Environment.NewLine
        |> Error
    else
    let client = new HttpClient() |> configureClient
    let args =
        endpoints
        |> List.map (fun e ->
            let sendMethodInfo = sendMethodInfo.MakeGenericMethod(e.ReturnType)
            FSharpValue.MakeFunction(
                e.FunctionType,
                fun arg ->
                    let result = sendMethodInfo.Invoke(null, [|client; e.Method; e.SerializationType; Uri(host, e.Path); arg|])
                    Convert.ChangeType(result, e.ReturnType)
            )
        )
        |> Array.ofList

    FSharpValue.MakeRecord(typeof<'Definition>, args)
    :?> 'Definition
    |> Ok
