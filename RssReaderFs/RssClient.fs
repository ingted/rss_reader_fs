﻿namespace RssReaderFs

open System

/// RSSクライアントクラス。
/// 純粋である RssReader に、自己更新、ファイルIOの機能を加えたもの。
type RssClient private (path: string) =
  let mutable reader =
    match path |> RssReader.Serialize.load with
    | Some rr -> rr
    | None -> failwithf "Invalid sources: %s" path

  let proj (item: RssItem) =
    item.Title

  member this.Reader = reader

  member this.AddSource(src) =
    reader <- reader |> RssReader.addSource src

  member this.RemoveSource(url) =
    reader <- reader |> RssReader.removeSource url

  member this.ReadItem(item) =
    reader <- reader |> RssReader.readItem item

  member this.UpdateAsync(pred) =
    async {
      let! (reader', items) = reader |> RssReader.updateAsync pred
      do reader <- reader'
      return items
    }

  member this.UpdateAllAsync =
    this.UpdateAsync (fun _ -> true)

  static member Create(path) =
    new RssClient(path)

  member this.Save() =
    reader |> RssReader.Serialize.save path