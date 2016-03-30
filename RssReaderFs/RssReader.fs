﻿namespace RssReaderFs

open System
open System.Collections.Generic
open FsYaml

module RssReader =
  let empty =
    {
      FeedMap         = Map.empty
      SourceMap       = Map.empty
      UnreadItems     = Set.empty
    }

  let internal feedMap (rr: RssReader) =
    rr.FeedMap

  let sourceMap (rr: RssReader) =
    rr.SourceMap

  let unreadItems (rr: RssReader) =
    rr.UnreadItems

  let allFeeds rr =
    rr.FeedMap
    |> Map.toArray
    |> Array.map snd

  /// The maximum source
  let allFeedSource rr: RssSource =
    rr
    |> allFeeds
    |> Array.map RssSource.ofFeed
    |> Set.ofArray
    |> (fun srcs -> RssSource.union ("ALL", srcs))

  let alreadyReadItems rr =
    rr
    |> allFeeds
    |> Array.map (fun src -> src.DoneSet)
    |> Array.fold (+) Set.empty

  let tryFindFeed url rr =
    rr |> feedMap |> Map.tryFind url

  let feedName url rr =
    let name =
      match rr |> tryFindFeed url with
      | Some { Name = name } -> name + " "
      | None -> ""
    in
      sprintf "%s<%s>" name (url |> string)

  let internal addFeed feed rr =
    { rr with FeedMap = rr |> feedMap |> Map.add (feed.Url) feed }

  let internal removeFeed url rr =
    { rr with FeedMap = rr |> feedMap |> Map.remove url }

  let updateFeeds feeds rr =
    let feedMap' =
      feeds
      |> Seq.fold
          (fun feedMap feed -> feedMap |> Map.add (feed.Url) feed)
          (rr |> feedMap)
    in
      { rr with FeedMap = feedMap' }

  let tryFindSource srcName rr =
    rr |> sourceMap |> Map.tryFind srcName

  let addSource src rr =
    let rr =
      match src with
      | Feed feed -> rr |> addFeed feed
      | _ -> rr
    in
      { rr with SourceMap = rr |> sourceMap |> Map.add (src |> RssSource.name) src }

  let removeSource srcName rr =
    match rr |> tryFindSource srcName with
    | None -> rr
    | Some src ->
        let rr =
          match src with
          | Feed feed -> rr |> removeFeed (feed.Url)
          | _ -> rr
        in
          { rr with SourceMap = rr |> sourceMap |> Map.remove srcName }

  let addUnreadItems items rr =
    { rr with UnreadItems = rr.UnreadItems + (items |> Set.ofSeq) }

  let readItem (item: RssItem) rr =
    let feedMap' =
      match rr |> feedMap |> Map.tryFind (item.Url) with
      | None -> rr |> feedMap
      | Some feed ->
          let feed' =
            { feed with DoneSet = feed.DoneSet |> Set.add item }
          in
            rr |> feedMap |> Map.add (feed.Url) feed'
    let unreadItems' =
      rr.UnreadItems
      |> Set.remove item
    in
      { rr with
          FeedMap         = feedMap'
          UnreadItems     = unreadItems'
      }

  let updateAsync src rr =
    async {
      let! (feeds', unreadItems) =
        src
        |> RssSource.ofUnread
        |> RssSource.fetchItemsAsync

      let rr =
        rr
        |> updateFeeds feeds'
        |> addUnreadItems unreadItems

      return (rr, unreadItems)
    }

  let updateAllAsync rr =
    rr |> updateAsync (rr |> allFeedSource)

  let toSpec rr =
    let feeds =
      rr |> allFeeds
    let srcSpecs =
      rr
      |> sourceMap
      |> Map.valueSet
      |> Set.map (RssSource.toSpec)
    in
      {
        Feeds           = feeds
        SourceSpecSet   = srcSpecs
      }

  let ofSpec (spec: RssReaderSpec) =
    let feedMap =
      spec.Feeds
      |> Array.map (fun feed -> (feed.Url, feed))
      |> Map.ofArray
    let rr =
      feedMap
      |> Map.fold (fun rr _ feed -> rr |> addSource (Feed feed)) empty
    let rr =
      spec.SourceSpecSet
      |> Set.map (RssSource.ofSpec feedMap)
      |> Set.fold (fun rr src -> rr |> addSource src) rr
    in rr

  let toJson rr =
    rr |> toSpec |> Yaml.customDump

  let ofJson json =
    json |> Yaml.customTryLoad<RssReaderSpec> |> Option.get |> ofSpec

  module Serialize =
    open System.IO

    let load path =
      try
        let json =
          File.ReadAllText(path)
        in
          json |> ofJson |> Some
      with
      | _ -> None

    let loadOrEmpty path =
      match load path with
      | Some rr -> rr
      | None -> empty

    let save path rr =
      let json =
        rr |> toJson
      in
        File.WriteAllText(path, json)
