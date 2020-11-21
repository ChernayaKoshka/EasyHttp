// TODO: Techncially, query strings can contain multiple values.
// For example, Value=1,2,3
// Should we allow `seq` serializaiton/deserialization in this case?
/// <Summary>
/// Contains the necessary functions for (de)serializing from/to a query string
/// </Summary>
[<RequireQualifiedAccess>]
module EasyHttp.Serializers.QueryString

open System
open System.Reflection
open FSharp.Reflection
open System.Net
open EasyHttp.Serializers.Utils

// TODO: Purely for organization, could be moved up?
/// <Summary>
/// Contains a variety of functions relevant to query string serialization. This is/should remain hidden in the signature file as it is an implementation detail.
/// </Summary>
module Serialize =
    /// <summary>
    /// Serializes the provided `obj` to a query string.
    /// </summary>
    /// <param name="toSerialize">The object to serialize</param>
    /// <returns>`Result<string, string>` depending on whether or not serialization was successful.</returns>
    let serialize (toSerialize: obj) =
        // has the potential to parse a serialize `None` value successfully
        // because the representation of unit/None are both `null`. This makes it
        // impossible to check if we were passed a unit vs None because a call to
        // .GetType() will kick a NullReferenceException. However, supporting
        // empty queries is more important than the potential of someone passing in a None
        // value (or, worse, an actual `null` 😬)
        if isNull toSerialize then
            Ok String.Empty
        else
            toSerialize.GetType()
            |> extractRecordValues true toSerialize
            |> Result.map toQueryString

// TODO: Purely for organization, could be moved up?
/// <Summary>
/// Contains a variety of functions relevant to query string deserialization. This is/should remain hidden in the signature file as it is an implementation detail.
/// </Summary>
module Deserialize =
    // TODO: Could be replaced with Microsoft.AspNetCore.Http.QueryString. Will that play nice with WASM?
    /// <summary>
    /// Splits and decodes a provided query string into its components.
    /// </summary>
    /// <param name="queryString">THe query string to split up</param>
    /// <returns>An array of tuples of `(key, value)`</returns>
    let fromQueryString (queryString: string) =
        queryString.TrimStart('?').Split('&')
        |> Array.map (fun pair ->
            let [| query; value |] = pair.Split('=')
            WebUtility.UrlDecode(query), WebUtility.UrlDecode(value)
        )

    /// <summary>
    /// Deserializes the query string into the provided record type.
    /// </summary>
    /// <param name="queryString">The query string containing values to deserialize from.</param>
    /// <typeparam name="'T">The record type to return if deserialization is successful.</typeparam>
    /// <returns>`Result<'T, string>` depending on if deserialization was successful or not.</returns>
    let deserialize<'T>(queryString: string) =
        let t = typeof<'T>
        if t = typeof<unit> then
            // need to return using Unchecked.defaultof<'T> (resulting in `unit` anyway!) in order to keep the compiler happy
            Ok <| Unchecked.defaultof<'T>
        else if isNull queryString then
            Error "Query string cannot be null when the type is not unit."
        else
        let values = fromQueryString queryString
        t
        |> getRecordFields
        |> Result.bind (areConstraintsSatisfied true t)
        |> Result.bind (fillPropertyValues<'T> values)

let serialize (o: obj) = Serialize.serialize o

// completely unnecessary for our purposes, but I'm leaving it here in case it becomes useful in the future
let deserialize<'T> (queryString: string) = Deserialize.deserialize<'T> queryString