﻿namespace RssReaderFs

module RssReader =
  type RssReader (initialFeeds: RssFeed []) =
    let mutable feeds = initialFeeds

    new () =
      RssReader()

    new (lastUpdate, sources: seq<RssSource>) =
      let feeds =
        sources
        |> Seq.map (Rss.emptyFeed lastUpdate)
        |> Seq.toArray
      RssReader(feeds)

    member this.Update() =
      let newFeeds =
        feeds
        |> Seq.map (Rss.updateFeedAsync)
        |> Async.Parallel
        |> Async.RunSynchronously
      feeds <- newFeeds
      
    // 全アイテムの時系列順
    member this.Timeline =
      feeds
      |> Seq.collect (fun feed -> feed.Items)
      |> Seq.toList
      |> List.sortBy (fun item -> item.Date)
      |> List.rev

    member this.Feeds =
      feeds

    member this.EmptyFeeds =
      feeds
      |> Array.map (fun feed -> 
          { feed
            with
              Items = []
              OldItems = []
          })
