[<AutoOpen>]
module EasyHttp.EasyHttp

open EasyHttp.Serializers
open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Reflection
open System
open System.IO
open System.Net
open System.Net.Http
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading.Tasks

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

        if not returnType.IsGenericType || returnType.GetGenericTypeDefinition() <> typedefof<Task<_>> then
            (endpoints, $"'{f.Name}' return type must be Task<_>" :: errors)
        else

        if argType <> typeof<unit> && argType |> FSharpType.IsRecord |> not then
            (endpoints, $"The argument of {f.Name} must be a 'Task<F# Record>' or Task<unit>." :: errors)
        else

        let returnType = returnType.GetGenericArguments().[0]
        let path = getAttributeContentsOrDefault f (fun (pa: PathAttribute) -> pa.Path) String.Empty
        let method = getAttributeContentsOrDefault f (fun (ma: MethodAttribute)-> ma.Method) HttpMethod.Post
        let serializationType =
            let defaultSerialization =
                if methodAllowsBody method then JsonSerialization
                else PathStringSerialization
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
    /// <param name="uriFragment">The URI fragment from the method</param>
    /// <param name="content">The content to serialize</param>
    /// <typeparam name="'ReturnType">The expected return type of the request (assumed JSON)</typeparam>
    /// <returns>Returns the response deserialized as JSON to the provided 'ReturnType</returns>
    static member Send (client: HttpClient) (method: HttpMethod) (serializationType: SerializationType) (requestUri: Uri) (uriFragment: string) (content: obj) : Task<'ReturnType> = task {
        let! response =
            match serializationType with
            | JsonSerialization ->
                let requestUri = Uri(requestUri, uriFragment)
                // TODO: Allow JsonSerializer to house serialization options? How would that work with an Attribute?
                let content = JsonSerializer.Serialize(content)
                new HttpRequestMessage(method, requestUri,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                )
            | PathStringSerialization ->
                let uriFragment =
                    match PathString.serialize uriFragment content with
                    | Ok fragment -> fragment
                    | Error err -> failwith err
                let requestUri = Uri(requestUri, uriFragment)
                new HttpRequestMessage(method, requestUri)
            |> client.SendAsync

        if typeof<'ReturnType> = typeof<unit> then
            return box () :?> 'ReturnType

        else
        let! stream = response.Content.ReadAsStreamAsync()
        if typeof<'ReturnType> = typeof<string> then
            use reader = new StreamReader(stream)
            let! body = reader.ReadToEndAsync()
            return box body :?> 'ReturnType

        else
        return! JsonSerializer.DeserializeAsync<'ReturnType>(stream)
    }
let sendMethodInfo = typeof<Http>.GetMethod(nameof Http.Send, BindingFlags.Static ||| BindingFlags.NonPublic)

// TODO: How about we verify the path doesn't have any optional values in it _before_ sending it off?
let makeApi< 'ApiDefinition > (baseUri: Uri) (client: HttpClient) =
    let t = typeof< 'ApiDefinition >
    if t |> FSharpType.IsRecord |> not then
        Error $"{t.AssemblyQualifiedName} must be a record."
    else
    let endpoints, errors = extractEndpoints t
    if errors.Length <> 0 then
        errors
        |> String.concat Environment.NewLine
        |> Error
    else
    let args =
        endpoints
        |> List.map (fun e ->
            let sendMethodInfo = sendMethodInfo.MakeGenericMethod(e.ReturnType)
            FSharpValue.MakeFunction(
                e.FunctionType,
                fun arg ->
                    sendMethodInfo.Invoke(null, [| client; e.Method; e.SerializationType; baseUri; e.Path; arg |])
            )
        )
        |> Array.ofList

    FSharpValue.MakeRecord(typeof< 'ApiDefinition >, args)
    :?> 'ApiDefinition
    |> Ok
