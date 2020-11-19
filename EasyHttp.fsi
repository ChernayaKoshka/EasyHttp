module EasyHttp.EasyHttp
open System
open System.Net.Http

val inline makeApi< ^Definition > : (HttpClient -> HttpClient) -> Result<'Definition, string>