#load @".paket/load/netstandard2.0/Google.Apis.Customsearch.v1.fsx"
#load @".paket/load/netstandard2.0/Suave.fsx"

open System
open System.Threading

open Google.Apis.Customsearch.v1
open Google.Apis.Services

open Newtonsoft.Json
open Newtonsoft.Json.Serialization

open Suave              
open Suave.Successful
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors 

let inline (^) f x = f x 

let nullableToOption (n : Nullable<_>) = 
  if n.HasValue 
  then Some n.Value 
  else None

type Item = 
  { DisplayLink: string
    Link: string
    FormattedUrl: string
    HtmlFormattedUrl: string
    Title: string
    HtmlTitle: string
    Snippet: string
    HtmlSnippet: string }

type Result = 
  { TotalCount: int64 option
    SearchTime: float option
    Items: Item list }

#load @".paket/load/netstandard2.0/dotenv.net.fsx"

open dotenv.net

let envPath = __SOURCE_DIRECTORY__ + @"/../.env";

DotEnv.Config(true, envPath)

let cseId = Environment.GetEnvironmentVariable  "CSE_ID"
let cseApiKey = Environment.GetEnvironmentVariable  "CSE_API_KEY"

let buildExactQuery = sprintf @"""%s""" 

let JSON value =
  let jsonSerializerSettings = JsonSerializerSettings()
  jsonSerializerSettings.ContractResolver <- CamelCasePropertyNamesContractResolver()
  jsonSerializerSettings.Formatting <- Formatting.Indented

  let result = JsonConvert.SerializeObject (value, jsonSerializerSettings)
  
  Writers.setMimeType "application/json; charset=utf-8"
  >=> OK result

let app =
    choose [
        path "/api/phrase" >=> 
          request ^fun r ->
            match r.queryParam "q" with
            | Choice2Of2 error -> 
                BAD_REQUEST error
            | Choice1Of2 query -> 
                fun context -> async {
                  let initializer = BaseClientService.Initializer(ApiKey = cseApiKey) 
                  use searchService = new CustomsearchService(initializer)

                  let request = searchService.Cse.List(buildExactQuery query)
                  request.Cx <- cseId

                  let top = 10L
                  let skip = 0L

                  request.Num <- Nullable<int64>(top) 
                  request.Start <- Nullable<int64>(1L + top + skip)

                  let! data = request.ExecuteAsync() |> Async.AwaitTask

                  let mapResultItem (i: Data.Result) =
                      {  DisplayLink = i.DisplayLink
                         Link = i.Link
                         FormattedUrl = i.FormattedUrl
                         HtmlFormattedUrl = i.HtmlFormattedUrl
                         Title = i.Title
                         HtmlTitle = i.HtmlTitle
                         Snippet = i.Snippet
                         HtmlSnippet = i.HtmlSnippet }

                  let total = nullableToOption data.SearchInformation.TotalResults
                  let searchTime = nullableToOption data.SearchInformation.SearchTime
                  let items = 
                    if isNull data.Items 
                      then List.empty 
                      else Seq.map mapResultItem data.Items |> Seq.toList

                  let result =
                      { TotalCount = total
                        SearchTime = searchTime
                        Items = items }

                  return! JSON result context
                }
           ]

let startServer () =
  let cts = new CancellationTokenSource()
  let config = 
    { defaultConfig with
        bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8084 ]
        cancellationToken = cts.Token }
  let listening, server = 
    startWebServerAsync config app
  Async.Start(server, cts.Token) |> ignore
  Async.RunSynchronously listening |> ignore
  cts

let stopServer (cts : CancellationTokenSource) =
  cts.Cancel true
  cts.Dispose()

let cts = startServer()

stopServer cts