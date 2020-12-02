[<AutoOpen>]
module EasyHttp.EasyHttp
open System
open System.Net.Http

/// <summary>
/// Populates the function definitions contained within a record of type `'Definition` using attributes+signature to invoke web requests.
/// </summary>
/// <param name="baseUri">The base URI to use for all requests</param>
/// <param name="client">The HttpClient instance to use for all subsequent requests</param>
/// <typeparam name="'Definition">The type of the record to construct. </typeparam>
/// <returns>A `Result<^Definition, string>` type depending on whether or not the creation was successful.</returns>
val makeApi< 'Definition > :
    baseUri: Uri ->
    client: HttpClient
        -> Result<'Definition, string>