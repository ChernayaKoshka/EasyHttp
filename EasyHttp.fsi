[<AutoOpen>]
module EasyHttp.EasyHttp
open System
open System.Reflection
open System.Net.Http

/// test
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
    val inline getAttributeContentsOrDefault< ^Attribute, ^Result when ^Attribute :> Attribute > :
        info        : PropertyInfo ->
        accessor    : (^Attribute ->  ^Result) ->
        defaultValue: ^Result
        ->  ^Result

    /// <summary>
    /// Checks if the provided method allows a body.
    /// </summary>
    /// <param name="method">The method to check</param>
    /// <returns>True if the method allows a body, false otherwise.</returns>
    val methodAllowsBody:
        method: HttpMethod
        -> bool

    /// <summary>
    /// Extracts endpoint data from a provided record. Using a combination of function signature and attributes (if any).
    /// </summary>
    /// <param name="recordType">The type of a record containing defined endpoints to extract.</param>
    /// <returns>A tuple of an endpoint list and an error list.</returns>
    val extractEndpoints:
        recordType: Type
        -> list<Endpoint> * list<string>

    /// <Summary>
    /// This class' only purpose is to contain the Send function. It needs to exist in a class in order to facilitate retrieving its definition
    /// and creating a generic method from that type definition.
    /// </Summary>
    type Http =
        private new: unit -> Http

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
        static member Send:
            client: HttpClient ->
            method: HttpMethod ->
            serializationType: SerializationType ->
            requestUri: Uri ->
            uriFragment: string ->
            content: obj
                -> 'ReturnType

    /// <summary>
    /// Holds the `Http.Send` method info, to be used later to dynamically create generic versions and invoke them.
    /// </summary>
    val sendMethodInfo: MethodInfo

/// <summary>
/// Populates the function definitions contained within a record of type `^Definition` using attributes+signature to invoke web requests.
/// </summary>
/// <param name="configureClient">Configures the HttpClient that will be used in the web requests. `id` can be provided if no such configuraiton is desired.</param>
/// <typeparam name="^Definition">The type of the record to construct. It _must_ implement a static member called `BaseUri` that returns a `Uri` that will be used as the base URI for all request.</typeparam>
/// <returns>A `Result<^Definition, string>` type depending on whether or not the creation was successful.</returns>
val inline makeApi< ^Definition when ^Definition : (static member BaseUri: Uri) > : (HttpClient -> HttpClient) -> Result<'Definition, string>