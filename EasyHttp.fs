[<AutoOpen>]
module EasyHttp.EasyHttp

open EasyHttp.Serializers
open FSharp.Control.Tasks
open FSharp.Reflection
open System
open System.IO
open System.Net
open System.Net.Http
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading.Tasks

[<Obsolete("This module is only exposed to support makeApi being inline in order to use SRTP. This is an implementation detail and should not be utilized.")>]
/// Do not use.
module Internal =
    let inline getAttributeContentsOrDefault< ^Attribute, ^Result when ^Attribute :> Attribute > (info: PropertyInfo) accessor (defaultValue: ^Result) =
        let attribute = Attribute.GetCustomAttribute(info, typeof< ^Attribute >)
        if isNull attribute then
            defaultValue
        else
            attribute
            :?> ^Attribute
            |> accessor

    let methodAllowsBody (method: HttpMethod) =
        match method.Method.ToLowerInvariant() with
        | "get" | "delete" | "trace" | "options" | "head" -> false
        | _ -> true

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
    type Http private () =
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

    let sendMethodInfo = typeof<Http>.GetMethod(nameof Http.Send)

#nowarn "44" // This construct is deprecated.
open Internal

// TODO: How about we verify the path doesn't have any optional values in it _before_ sending it off?
let inline makeApi< ^Definition when ^Definition : (static member BaseUri: Uri) > (client: HttpClient) =
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
    let args =
        endpoints
        |> List.map (fun e ->
            let sendMethodInfo = sendMethodInfo.MakeGenericMethod(e.ReturnType)
            FSharpValue.MakeFunction(
                e.FunctionType,
                fun arg ->
                    sendMethodInfo.Invoke(null, [| client; e.Method; e.SerializationType; hostUri; e.Path; arg |])
            )
        )
        |> Array.ofList

    FSharpValue.MakeRecord(typeof< ^Definition >, args)
    :?> ^Definition
    |> Ok
