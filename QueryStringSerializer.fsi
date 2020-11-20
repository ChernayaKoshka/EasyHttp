module EasyHttp.QueryStringSerializer
/// <summary>
/// Serializes the provided `obj` to a query string.
/// </summary>
/// <param name="toSerialize">The object to serialize</param>
/// <returns>`Result<string, string>` depending on whether or not serialization was successful.</returns>
val serialize : (obj -> Result<string,string>)

/// <summary>
/// Deserializes the query string into the provided record type.
/// </summary>
/// <param name="queryString">The query string containing values to deserialize from.</param>
/// <typeparam name="'T">The record type to return if deserialization is successful.</typeparam>
/// <returns>`Result<'T, string>` depending on if deserialization was successful or not.</returns>
val deserialize<'a> : (string -> Result<'a,string>)