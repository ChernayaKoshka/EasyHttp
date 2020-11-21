module EasyHttp.Serializers.Utils

open System
open System.Reflection
open FSharp.Reflection
open System.Net

// could greatly benefit from memoization?
let getRecordFields (recordType: Type) =
    if FSharpType.IsRecord(recordType) then
        recordType
        |> FSharpType.GetRecordFields
        |> Ok
    else
        Error "Only records/unit are supported for query string (de)serialization."

let isOptionType (typ: Type) =
    typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>

let extractOptionValue (optionType: Type) (instance: obj) =
    if isNull instance then
        None
    else
        FSharpValue.GetUnionFields(instance, optionType)
        |> snd
        |> Array.head
        |> Some

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
        |> sprintf "All properties of type '%s' must be primitives, string, or an option type. Offending properties as follows: %s" recordType.AssemblyQualifiedName
        |> Error

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

let extractRecordValues (instance: obj) (typ: Type) =
    typ
    |> getRecordFields
    |> Result.bind (areConstraintsSatisfied typ)
    |> Result.map (extractPropertyValues instance)

// TODO: Could be replaced with Microsoft.AspNetCore.Http.QueryString. Will that play nice with WASM?
let toQueryString (vals: (string * string) seq) =
    vals
    |> Seq.map (fun (param, value) ->
        sprintf "%s=%s" (WebUtility.UrlEncode(param)) (WebUtility.UrlEncode(value)))
    |> String.concat "&"
    |> sprintf "?%s"