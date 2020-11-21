/// <Summary>
/// Contains a variety of reflection helpers to facilitate serialization/deserialization
/// </Summary>
module EasyHttp.Serializers.Utils

open System
open System.Reflection
open FSharp.Reflection
open System.Net

/// <summary>
/// Gets the record fields if the provided record type is a valid F# record.
/// </summary>
/// <param name="recordType">The record type to retrieve the fields from.</param>
/// <returns/>
val getRecordFields:
    recordType: Type
        -> Result<PropertyInfo array, string>

/// <summary>
/// Returns true if the provided <paramref name="typ"/> is an `Option` type
/// </summary>
/// <param name="typ">The type to check</param>
/// <returns/>
val isOptionType:
    typ: Type
        -> bool

/// <summary>
/// Extracts the inner value of an `Option<_>`
/// </summary>
/// <param name="optionType">The type definition of the option</param>
/// <param name="instance">An instance of the option type</param>
/// <returns>`Some obj` if the option is `Some`, otherwise `None`.</returns>
val extractOptionValue:
    optionType: Type ->
    instance  : obj
        -> option<obj>

/// <summary>
/// Checks if the provided type can be serialized to a query string.
/// </summary>
/// <param name="typ">The typ to check</param>
/// <returns/>
val isTypeSerializable:
    typ: Type
        -> bool

/// <summary>
/// Verifies all of the provided `PropertyInfo`s are serializable.
/// </summary>
/// <param name="recordType">The record type to check</param>
/// <param name="props">A list of properties on the record to check</param>
/// <returns/>
val areConstraintsSatisfied:
    recordType: Type ->
    props     : PropertyInfo array
        -> Result<PropertyInfo array, string>

/// <summary>
/// Extracts all of the values from the `obj` using the provided `PropertyInfo`s
/// </summary>
/// <param name="instance">The object to retrieve values from.</param>
/// <param name="props">An array of properties to extract from the provided `instance`.</param>
/// <returns>A tuple of `(property name, property value to string)`</returns>
val extractPropertyValues:
    instance: obj ->
    props   : PropertyInfo array
        -> (string * string) array

/// <summary>
/// Fills the provided record type's values using the provided values / `PropertyInfo`s
/// </summary>
/// <param name="values">An array of tuples of `(property name, string value)` that will be used to construct the record from.</param>
/// <param name="props">The properties of the provided record to search for and populate.</param>
/// <typeparam name="'T">The type of record to construct</typeparam>
/// <returns>`Result<'T, string>` depending on whether or not the record was successfully constructed.</returns>
val fillPropertyValues:
    values: (string * string) array ->
    props : PropertyInfo array
        -> Result<'T,string>

/// <summary>
/// Extracts all of the values from a given record (primitive values only).
/// </summary>
/// <param name="instance">The record object to extract from</param>
/// <param name="typ">The type of <seeparam name="instance"/></param>
/// <returns>`Ok(property name, property value)` or `Error (errorMessage)`</returns>
val extractRecordValues:
    instance: obj  ->
    typ     : Type
        -> Result<(string * string) array,string>

/// <summary>
/// Using the provided tuples, it will create a query string.
/// </summary>
/// <param name="vals">The provided tuples to create a query string from.</param>
/// <returns>A query string in the format of `?key1=val1&key2=val2`</returns>
val toQueryString:
    vals: seq<string * string>
        -> string