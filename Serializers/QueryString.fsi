module EasyHttp.Serializers.QueryString
/// <summary>
/// Serializes the provided `obj` to a query string.
/// </summary>
/// <param name="toSerialize">The object to serialize</param>
/// <returns>`Result<string, string>` depending on whether or not serialization was successful.</returns>
val serialize :
    record: obj
        -> Result<string,string>

/// <summary>
/// Deserializes the query string into the provided record type.
/// </summary>
/// <param name="queryString">The query string containing values to deserialize from.</param>
/// <typeparam name="'T">The record type to return if deserialization is successful.</typeparam>
/// <returns>Result containing the deserialized object or an error string.</returns>
val deserialize<'T> :
    queryString: string
        -> Result<'T, string>