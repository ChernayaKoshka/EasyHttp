module EasyHttp.Serializers.PathString

/// <summary>
/// Serializes the provided `obj` to a query string.
/// </summary>
/// <param name="toSerialize">The object to serialize</param>
/// <returns>`Result<string, string>` depending on whether or not serialization was successful.</returns>
val serialize:
    pathStringFormat: string ->
    record: obj
        -> Result<string,string>

/// <summary>
/// NOT YET SUPPORTED (maybe never supported, tbh. Seems really hard for something I/we might not use.)
/// Deserializes the path string into the provided record type.
/// </summary>
/// <param name="pathString">The path string containing values to deserialize from.</param>
/// <param name="pathStringFormat">The path string format used to define where the field(s) are located.</param>
/// <typeparam name="'T">The record type to return if deserialization is successful.</typeparam>
/// <returns>Result containing the deserialized object or an error string.</returns>
// val deserialize<'T> :
//     pathStringFormat: string ->
//     pathString: string
//         ->  Result<'T, string>