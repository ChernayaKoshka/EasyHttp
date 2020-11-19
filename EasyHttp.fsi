module EasyHttp.EasyHttp
open System
open System.Net.Http

val makeApi<'Definition> :  Uri -> (HttpClient -> HttpClient) -> Result<'Definition, string>