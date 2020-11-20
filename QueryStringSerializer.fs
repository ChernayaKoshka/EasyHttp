// TODO: Techncially, query strings can contain multiple values.
// For example, Value=1,2,3
// Should we allow `seq` serializaiton/deserialization in this case?
/// <Summary>
/// Contains the necessary functions for (de)serializing from/to a query string
/// </Summary>
module EasyHttp.QueryStringSerializer

open System
open System.Reflection
open FSharp.Reflection
open System.Net

/// <Summary>
/// Contains a variety of reflection helpers to facilitate serialization/deserialization
/// </Summary>
module Utils =
    /// <summary>
    /// Gets the record fields if the provided record type is a valid F# record.
    /// </summary>
    /// <param name="recordType">The record type to retrieve the fields from.</param>
    /// <returns>`Result<PropertyInfo array, string>`</returns>
    let getRecordFields (recordType: Type) =
        if FSharpType.IsRecord(recordType) then
            recordType
            |> FSharpType.GetRecordFields
            |> Ok
        else
            Error "Only records/unit are supported for query string (de)serialization."

    /// <summary>
    /// Returns true if the provided `typ` is an `Option<_>`
    /// </summary>
    /// <param name="typ">The type to check</param>
    /// <returns/>
    let isOptionType (typ: Type) =
        typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>

    /// <summary>
    /// Checks if the provided type can be serialized to a query string.
    /// </summary>
    /// <param name="typ">The typ to check</param>
    /// <returns/>
    let isTypeSerializable (typ: Type) =
        let rec checkType (checkingOptionCase: bool) (typ: Type) =
            if typ.IsPrimitive || typ = typeof<string> then
                true
            // keeping code block like this for clarity (hopefully)
            // fsharplint:disable-next-line Hints
            else if
                // don't want to return a false positive on a type like Option<Option<string>>
                not checkingOptionCase
                && isOptionType typ
                && checkType true (typ.GetGenericArguments().[0]) then
                true
            else
                false
        checkType false typ

    /// <summary>
    /// Verifies all of the provided `PropertyInfo`s are serializable.
    /// </summary>
    /// <param name="recordType">The record type to check</param>
    /// <param name="props">A list of properties on the record to check</param>
    /// <returns/>
    let areConstraintsSatisfied (recordType: Type) (props: PropertyInfo array) =
        let satisfied =
            props
            |> Array.map (fun prop ->
                (prop, isTypeSerializable prop.PropertyType)
            )
        if Array.forall (snd >> (=) true) satisfied then
            Ok props
        else
            satisfied
            |> Array.filter (snd >> (=) false)
            |> Array.map (fun (prop, _) -> prop.Name)
            |> String.concat ", "
            |> sprintf "All properties of type '%s' must be primitives or string. Offending properties as follows: %s" recordType.AssemblyQualifiedName
            |> Error
open Utils

// TODO: Purely for organization, could be moved up?
/// <Summary>
/// Contains a variety of functions relevant to query string serialization. This is/should remain hidden in the signature file as it is an implementation detail.
/// </Summary>
module Serialize =
    // TODO: Could be replaced with Microsoft.AspNetCore.Http.QueryString. Will that play nice with WASM?
    /// <summary>
    /// Using the provided tuples, it will create a query string.
    /// </summary>
    /// <param name="vals">The provided tuples to create a query string from.</param>
    /// <returns>A query string in the format of `?key1=val1&key2=val2`</returns>
    let toQueryString (vals: (string * string) seq) =
        vals
        |> Seq.map (fun (param, value) ->
            sprintf "%s=%s" (WebUtility.UrlEncode(param)) (WebUtility.UrlEncode(value)))
        |> String.concat "&"
        |> sprintf "?%s"

    /// <summary>
    /// Extracts the inner value of an `Option<_>`
    /// </summary>
    /// <param name="optionType">The type definition of the option</param>
    /// <param name="instance">An instance of the option type</param>
    /// <returns>`Some obj` if the option is `Some`, otherwise `None`.</returns>
    let extractOptionValue (optionType: Type) (instance: obj) =
        if isNull instance then
            None
        else
            FSharpValue.GetUnionFields(instance, optionType)
            |> snd
            |> Array.head
            |> Some

    /// <summary>
    /// Extracts all of the values from the `obj` using the provided `PropertyInfo`s
    /// </summary>
    /// <param name="instance">The object to retrieve values from.</param>
    /// <param name="props">An array of properties to extract from the provided `instance`.</param>
    /// <returns>A tuple of `(property name, property value to string)`</returns>
    let extractPropertyValues (instance: obj) (props: PropertyInfo array) =
        props
        |> Array.choose (fun prop ->
            let value = prop.GetValue(instance)
            if isOptionType prop.PropertyType then
                value
                |> extractOptionValue prop.PropertyType
                |> Option.map (fun value -> (prop.Name, string value))
            else
                Some (prop.Name, string value)
        )

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
            let t = toSerialize.GetType()
            t
            |> getRecordFields
            |> Result.bind (areConstraintsSatisfied t)
            |> Result.map (extractPropertyValues toSerialize)
            |> Result.map toQueryString

// TODO: Purely for organization, could be moved up?
/// <Summary>
/// Contains a variety of functions relevant to query string deserialization. This is/should remain hidden in the signature file as it is an implementation detail.
/// </Summary>
module Deserialize =
    // caching on startup so we don't keep making calls to GetUnionCases/MakeUnion every time
    // precomputing constructors

    /// <summary>
    /// Precomputed option constructors for primitive/string types.
    /// </summary>
    let primOptCtors =
        [
            typeof<bool option>
            typeof<byte option>
            typeof<sbyte option>
            typeof<char option>
            typeof<decimal option>
            typeof<double option>
            typeof<single option>
            typeof<int32 option>
            typeof<uint32 option>
            typeof<int64 option>
            typeof<uint64 option>
            typeof<int16 option>
            typeof<uint16 option>
            typeof<string option>
        ]
        |> List.map (fun typ ->
            typ,
            typ
            |> FSharpType.GetUnionCases
            |> Array.find (fun uc -> uc.Name = "Some")
            |> FSharpValue.PreComputeUnionConstructor
        )
        |> dict

    /// <summary>
    /// Fills the provided record type's values using the provided values / `PropertyInfo`s
    /// </summary>
    /// <param name="values">An array of tuples of `(property name, string value)` that will be used to construct the record from.</param>
    /// <param name="props">The properties of the provided record to search for and populate.</param>
    /// <typeparam name="'T">The type of record to construct</typeparam>
    /// <returns>`Result<'T, string>` depending on whether or not the record was successfully constructed.</returns>
    let fillPropertyValues<'T> (values: (string * string) array) (props: PropertyInfo array) =
        let t = typeof<'T>
        let valueMap = Map.ofArray values
        // we know that all values need to be accounted for in this case
        if FSharpType.IsRecord(t) then
            let recordFields, errors =
                props
                |> Array.fold (fun (recordFields, errors) prop ->
                    match Map.tryFind prop.Name valueMap, isOptionType prop.PropertyType with
                    | Some value, true ->
                        (primOptCtors.[prop.PropertyType] [| Convert.ChangeType(value, prop.PropertyType.GetGenericArguments().[0]) |] :: recordFields, errors)
                    | Some value, false ->
                        (Convert.ChangeType(value, prop.PropertyType) :: recordFields, errors)
                    | None, true ->
                        (box None :: recordFields, errors)
                    | None, false ->
                        (recordFields, $"'{prop.Name}' was not found in the supplied values and was not optional." :: errors)
                ) (List.empty, List.empty)
                |> fun (a, b) -> List.rev a, List.rev b

            if errors.Length <> 0 then
                errors
                |> String.concat Environment.NewLine
                |> Error
            else
            // TODO: could potentially cache record constructors as they come in?
            FSharpValue.MakeRecord(t, Array.ofList recordFields)
            :?> 'T
            |> Ok
        else
        Error "'T must be an F# Record type or unit!"

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
        |> Result.bind (areConstraintsSatisfied t)
        |> Result.bind (fillPropertyValues<'T> values)

/// <summary>
/// Serializes the provided `obj` to a query string.
/// </summary>
/// <param name="toSerialize">The object to serialize</param>
/// <returns>`Result<string, string>` depending on whether or not serialization was successful.</returns>
let serialize (o: obj) = Serialize.serialize o


/// <summary>
/// Deserializes the query string into the provided record type.
/// </summary>
/// <param name="queryString">The query string containing values to deserialize from.</param>
/// <typeparam name="'T">The record type to return if deserialization is successful.</typeparam>
/// <returns>`Result<'T, string>` depending on if deserialization was successful or not.</returns>
// completely unnecessary for our purposes, but I'm leaving it here in case it becomes useful in the future
let deserialize<'T> = Deserialize.deserialize<'T>