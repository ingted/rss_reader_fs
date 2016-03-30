﻿namespace RssReaderFs

open System

module RssFeed =
  let doneSet (feed: RssFeed) =
    feed.DoneSet

  let create name (url: string) =
    {
      Name        = name
      Url         = Url.ofString (url)
      DoneSet     = Set.empty
    }
    
  let downloadAsync (feed: RssFeed) =
    async {
      let url = feed.Url
      let! xml = Net.downloadXmlAsync(url)
      return (xml |> RssItem.parseXml url)
    }

  let updateAsync feed =
    async {
      let! items = feed |> downloadAsync

      // 読了済みのものと分離する
      let (dones, undones) =
        items
        |> Seq.toArray
        |> Array.partition (fun item -> feed.DoneSet |> Set.contains item)

      let feed =
        { feed with
            DoneSet = dones |> Set.ofArray
        }

      return (feed, undones)
    }
