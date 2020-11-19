// TODO: Techncially, query strings can contain multiple values.
// For example, Value=1,2,3
// Should we allow `seq` serializaiton/deserialization in this case?
module EasyHttp.QueryStringSerializer

open System
open System.Reflection
open FSharp.Reflection
open System.Net

module Utils =
    let getRecordFields (recordType: Type) =
        if FSharpType.IsRecord(recordType) then
            recordType
            |> FSharpType.GetRecordFields
            |> Ok
        else
            Error "Only records/unit are supported for query string (de)serialization."

    let isOptionType (typ: Type) =
        typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>

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
            |> sprintf "All properties of type '%s' must be primitives or string. Offending properties as follows: %s" recordType.AssemblyQualifiedName
            |> Error
open Utils

// TODO: Purely for organization, could be moved up?
module Serialize =
    // TODO: Could be replaced with Microsoft.AspNetCore.Http.QueryString. Will that play nice with WASM?
    let toQueryString (vals: (string * string) seq) =
        vals
        |> Seq.map (fun (param, value) ->
            sprintf "%s=%s" (WebUtility.UrlEncode(param)) (WebUtility.UrlEncode(value)))
        |> String.concat "&"
        |> sprintf "?%s"

    let extractOptionValue (instance: obj) (prop: PropertyInfo) =
        let value = prop.GetValue(instance)
        if isNull value then
            None
        else
            FSharpValue.GetUnionFields(value, prop.PropertyType)
            |> snd
            |> Array.head
            |> Some

    let extractPropertyValues (instance: obj) (props: PropertyInfo array) =
        props
        |> Array.choose (fun prop ->
            if isOptionType prop.PropertyType then
                prop
                |> extractOptionValue instance
                |> Option.map (fun value -> (prop.Name, string value))
            else
                Some (prop.Name, string (prop.GetValue(instance)))
        )

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
module Deserialize =
    // caching on startup so we don't keep making calls to GetUnionCases/MakeUnion every time
    // precomputing constructors
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

    // TODO: Could be replaced with Microsoft.AspNetCore.Http.QueryString. Will that play nice with WASM?
    let fromQueryString (queryString: string) =
        queryString.TrimStart('?').Split('&')
        |> Array.map (fun pair ->
            let [| query; value |] = pair.Split('=')
            WebUtility.UrlDecode(query), WebUtility.UrlDecode(value)
        )

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

let serialize (o: obj) = Serialize.serialize o

// completely unnecessary for our purposes, but I'm leaving it here in case it becomes useful in the future
let deserialize<'T> = Deserialize.deserialize<'T>