[<AutoOpen>]
module Prelude =
  let inline (^) f x = f x 

#r "nuget: dotenv.net, 3.1.1"
#r "nuget: Google.Apis.Customsearch.v1, 1.49.0.2084"
#r "nuget: FSharp.SystemTextJson, 0.19.13"
#r "nuget: Suave, 2.6.2"


open System
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading

open Google.Apis.Customsearch.v1
open Google.Apis.Services

open Suave              
open Suave.Successful
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors 

open dotenv.net

DotEnv.Load()

let cseId = Environment.GetEnvironmentVariable "CSE_ID"
let cseApiKey = Environment.GetEnvironmentVariable "CSE_API_KEY"

let host = Environment.GetEnvironmentVariable "HOST"
let port = Environment.GetEnvironmentVariable "PORT" |> Int32.Parse

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
  { TotalCount: int
    SearchTime: float option
    Items: Item list }

let JSON value =
  let options = JsonSerializerOptions()
  options.Converters.Add(JsonFSharpConverter())
  options.WriteIndented <- true
  options.PropertyNameCaseInsensitive <- true
  options.Encoder <- JavaScriptEncoder.UnsafeRelaxedJsonEscaping

  let result = JsonSerializer.Serialize (value, options)
  
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

                  let request = searchService.Cse.List()
                  request.Cx <- cseId
                  request.ExactTerms <- query

                  let top = 10
                  let skip = 0

                  request.Num <- top
                  request.Start <- 1 + top + skip

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

                  let _, total = Int32.TryParse data.SearchInformation.TotalResults
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
        bindings = [ HttpBinding.createSimple HTTP host port ]
        cancellationToken = cts.Token }
  let listening, server = 
    startWebServerAsync config app
  Async.Start(server, cts.Token) |> ignore
  Async.RunSynchronously listening |> ignore

  let stopServer() =
    cts.Cancel true
    cts.Dispose()
    printfn "Server stopped."

  stopServer

let stopServer = startServer()

// stopServer()