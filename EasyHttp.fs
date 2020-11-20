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

[<Obsolete("This module is only exposed to support makeApi being inline in order to use SRTP. This is an implementation detail and should not be utilized.")>]
/// Do not use.
module Internal =
    /// <summary>
    /// Retrieves the contents of an attribute using the provided accessor. If the attribute is not present, it will return the defaultValue provided.
    /// </summary>
    /// <param name="info">The property info to retrieve an attribute from</param>
    /// <param name="accessor">The function to access the attribute contents if retrieved</param>
    /// <param name="defaultValue"></param>
    /// <typeparam name="^Attribute">The attribute type to retrieve.</typeparam>
    /// <typeparam name="^Result">The result/default value type</typeparam>
    /// <returns>The contents of the attribute as determined by the accessor or the default value.</returns>
    let inline getAttributeContentsOrDefault< ^Attribute, ^Result when ^Attribute :> Attribute > (info: PropertyInfo) accessor (defaultValue: ^Result) =
        let attribute = Attribute.GetCustomAttribute(info, typeof< ^Attribute >)
        if isNull attribute then
            defaultValue
        else
            attribute
            :?> ^Attribute
            |> accessor

    /// <summary>
    /// Checks if the provided method allows a body.
    /// </summary>
    /// <param name="method">The method to check</param>
    /// <returns>True if the method allows a body, false otherwise.</returns>
    let methodAllowsBody (method: HttpMethod) =
        match method.Method.ToLowerInvariant() with
        | "get" | "delete" | "trace" | "options" | "head" -> false
        | _ -> true

    /// <summary>
    /// Extracts endpoint data from a provided record. Using a combination of function signature and attributes (if any).
    /// </summary>
    /// <param name="recordType">The type of a record containing defined endpoints to extract.</param>
    /// <returns>A tuple of an endpoint list and an error list.</returns>
    let extractEndpoints (recordType: Type) =
        recordType
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

            if serializationType = JsonSerialization && (methodAllowsBody >> not) method then
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

    /// <Summary>
    /// This class' only purpose is to contain the Send function. It needs to exist in a class in order to facilitate retrieving its definition
    /// and creating a generic method from that type definition.
    /// </Summary>
    type Http private () =
        /// <summary>
        /// Sends an HTTP request, serializing the content either a JSON body or a query string depending on the provided serialization type.
        /// Regardless of the serialization type, it will deserialize any response as JSON.
        /// </summary>
        /// <param name="client">The provided client to make the request with.</param>
        /// <param name="method">The method to use.</param>
        /// <param name="serializationType">Determines how Send should serialize the provided content.</param>
        /// <param name="requestUri">The URI to make a request to</param>
        /// <param name="content">The content to serialize</param>
        /// <typeparam name="'ReturnType">The expected return type of the request (assumed JSON)</typeparam>
        /// <returns>Returns the response deserialized as JSON to the provided 'ReturnType</returns>
        static member Send<'ReturnType> (client: HttpClient) (method: HttpMethod) (serializationType: SerializationType) (requestUri: Uri) (content: obj) =
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
                JsonSerializer.Deserialize<'ReturnType>(reader.ReadToEnd())

    /// <summary>
    /// Holds the `Http.Send` method info, to be used later to dynamically create generic versions and invoke them.
    /// </summary>
    let sendMethodInfo = typeof<Http>.GetMethod(nameof Http.Send, BindingFlags.NonPublic ||| BindingFlags.Static)

#nowarn "44" // This construct is deprecated.
open Internal

/// <summary>
/// Populates the function definitions contained within a record of type `^Definition` using attributes+signature to invoke web requests.
/// </summary>
/// <param name="configureClient">Configures the HttpClient that will be used in the web requests. `id` can be provided if no such configuraiton is desired.</param>
/// <typeparam name="^Definition">The type of the record to construct. It _must_ implement a static member called `BaseUri` that returns a `Uri` that will be used as the base URI for all request.</typeparam>
/// <returns>A `Result<^Definition, string>` type depending on whether or not the creation was successful.</returns>
let inline makeApi< ^Definition when ^Definition : (static member BaseUri: Uri) > (configureClient: HttpClient -> HttpClient) =
    let t = typeof< ^Definition >
    // because we're not passing in an instance of the type, we can't use SRTP syntax to access it
    // however, SRTP have _guaranteed_ that it exists on the record! :D
    let hostUri = t.GetProperty("BaseUri").GetValue(null) :?> Uri
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
                    let result = sendMethodInfo.Invoke(null, [|client; e.Method; e.SerializationType; Uri( hostUri, e.Path); arg|])
                    Convert.ChangeType(result, e.ReturnType)
            )
        )
        |> Array.ofList

    FSharpValue.MakeRecord(typeof< ^Definition >, args)
    :?> ^Definition
    |> Ok
