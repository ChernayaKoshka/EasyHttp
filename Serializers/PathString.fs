[<RequireQualifiedAccess>]
module EasyHttp.Serializers.PathString

open EasyHttp.Serializers.Utils

open System
open System.Collections.Concurrent
open System.Reflection
open FSharp.Reflection
open System.Text.RegularExpressions
open System.Net

(*
    NOTES:
        Query string format...
            fills in 1:1
            /{memberName}/{memberName2}/
            fills in the fields in the order they appear in the record...?
                - Anonymous records are sorted (https://github.com/dotnet/fsharp/issues/6422), hm...
                - Maybe I'll just make a note of it to use actual F# records when pertinent
            /{!ordered!}
            fills in fields not put in a path w/ query string value
            {!query!}
*)

let [<Literal>] OrderedPathString = "{!ordered!}"
let [<Literal>] QueryStringCapture = "{!query!}"

let extractPathParams =
    let cache = new ConcurrentDictionary<string, Result<string array, string>>()
    // not an expensive function, but implementing caching is fun
    fun (pathStringFormat: string) ->
        match cache.TryGetValue(pathStringFormat) with
        | (true, result) ->
            result
        | (false, _) ->
            let result =
                try
                    Regex.Matches(pathStringFormat, "{(\w+)}", RegexOptions.None, TimeSpan.FromMilliseconds(100.))
                    |> Array.ofSeq
                    |> Array.map (fun m -> m.Groups.[1].Value)
                    |> Ok
                with
                | :? RegexMatchTimeoutException ->
                    Error "extractPathParams took too long"
            cache.[pathStringFormat] <- result
            result

let inline verifyNoEmptyPathValues (values: (string * string option) array) =
    match Array.filter (snd >> (=) None) values with
    | [| |] ->
        values
        |> Array.map (fun (key, value) ->
            (key, value.Value))
        |> Ok
    | emptyValues ->
        emptyValues
        |> Array.map (fst >> sprintf "'%s'")
        |> String.concat ", "
        |> sprintf "Empty path values are not supported. Offending fields follow: %s"
        |> Error

let applyOrderedPathString (pathStringFormat: string) (values: (string * string option) array) =
    if pathStringFormat.Contains(OrderedPathString) then
        values
        |> verifyNoEmptyPathValues
        |> Result.map (fun values ->
            let ordered =
                values
                |> Array.map (snd >> WebUtility.UrlEncode)
                |> String.concat "/"
            pathStringFormat.Replace(OrderedPathString, ordered)
        )
    else
    Ok pathStringFormat

let applyQueryString (queryValues: (string * string option) array) (pathString: string) =
    if queryValues.Length = 0 || pathString.EndsWith(QueryStringCapture) |> not then
        pathString
    else
        let trimmedUri =
            Regex
                .Replace(
                    pathString,
                    $"{QueryStringCapture}$",
                    String.Empty)
                .TrimEnd('/')
        trimmedUri + toQueryString queryValues

let applyPathParams pathValues pathStringFormat =
    pathValues
    |> Array.fold (fun (pathString: string) (name, value) ->
        pathString.Replace($"{{%s{name}}}", WebUtility.UrlEncode(value))
    ) pathStringFormat

let partitionPathAndQueryString values pathParams =
    let pathValues, queryStringValues =
        values
        |> Array.partition (fun (key, value) ->
            Array.contains key pathParams
        )
    pathValues
    |> verifyNoEmptyPathValues
    |> Result.map (fun pv -> (pv, queryStringValues))

let validatePopulatedPath (pathString: string) =
    if pathString.Contains("{!") then
        Error $"'{pathString}' had leftover special replacement markers."
    else if pathString.Contains("{") then
        pathString
        |> extractPathParams
        |> Result.mapError (fun errStr ->
            $"Error extracting leftover params: {errStr}"
        )
        |> Result.bind (
            (Array.map (sprintf "'%s'"))
            >> String.concat ", "
            >> (sprintf "The following parameters were missing from the record: %s")
            >> Error)
    else if Uri.IsWellFormedUriString(pathString, UriKind.Relative) |> not then
        Error $"Populated uri '{pathString}' is not a well formed uri string!"
    else
        Ok pathString

let populatePath (pathStringFormat: string) (values: (string * string option) array) =
    applyOrderedPathString pathStringFormat values
    |> Result.bind (fun ordered ->
        ordered
        |> extractPathParams
        |> Result.bind (partitionPathAndQueryString values)
        |> Result.map (fun (pathParams, queryParams) ->
            ordered
            |> applyPathParams pathParams
            |> applyQueryString queryParams
        )
        |> Result.bind validatePopulatedPath
    )

let serialize (pathStringFormat: string) (record: obj): Result<string, string> =
    if isNull record then
        Ok pathStringFormat
    else
        record.GetType()
        |> extractRecordValues record
        |> Result.bind (populatePath pathStringFormat)

let deserialize<'T> (pathStringFormat: string) (pathString: string): Result<'T, string> =
    raise <| NotSupportedException("This is not yet supported as I am extraordinarily lazy")