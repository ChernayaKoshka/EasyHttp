module EasyHttp.EasyHttp
open System
open System.Reflection
open System.Net.Http

module Internal =
    val inline getAttributeContentsOrDefault< ^Attribute, ^Result when ^Attribute :> Attribute > :
        info        : PropertyInfo ->
        accessor    : (^Attribute ->  ^Result) ->
        defaultValue: ^Result
        ->  ^Result

    val methodAllowsBody:
        method: HttpMethod
        -> bool

    val extractEndpoints:
        recordType: Type
        -> list<Endpoint> * list<string>

    type Http =
        private new: unit -> Http
        static member Send: client: HttpClient-> method: HttpMethod-> serializationType: SerializationType-> requestUri: Uri-> content: obj -> 'ReturnType

    val sendMethodInfo: MethodInfo

val inline makeApi< ^Definition when ^Definition : (static member BaseUri: Uri) > : (HttpClient -> HttpClient) -> Result<'Definition, string>