module EasyHttp.QueryStringSerializer

val serialize : (obj -> Result<string,string>)
val deserialize<'a> : (string -> Result<'a,string>)