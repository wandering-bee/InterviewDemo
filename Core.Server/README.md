# 🖥️ TcpServer — SledChannel 開発用バーチャルサーバ

---

### 🔍 GUI

![Demo UI](https://raw.githubusercontent.com/wandering-bee/InterviewDemo/main/img/Saver.png)

---

## 🚩 目的と概要
TcpServer は SledChannel の開発・検証専用に設計した軽量な仮想デバイスエミュレータです。  
ローカルで起動するだけで、認証付き ASCII／Shift-JIS テキストプロトコルを模擬し、  
多クライアント・高頻度通信・タイムアウト挙動を手軽に再現できます。

## 🔑 プロトコル仕様

| 項目         | 内容                                 |
|--------------|--------------------------------------|
| 最大接続数   | 6 クライアント                        |
| フレーミング | CR 又は CRLF 末尾                    |
| エンコード   | Shift-JIS（RD **** 系）／ASCII       |
| 認証         | `HELLO <secret>` → `OK\r\n`         |
| 切断         | `BYE <secret>` → `BYE\r\n`         |
| コマンド辞書 | `RD 3001〜RD 6000` → `0\r\n`        |
| 未定義       | `?\r\n`                             |
| 典型応答     | `PING` → `OK\r\n`                   |

## ⚙️ 実装ポイント

- PipeReader による CR／CRLF 双対応フレーム分割  
- クライアントごとに CancellationTokenSource を管理し、タイムアウト・キャンセルを即時伝搬  
- 全応答を辞書化してロックレス高速送出
  全コマンド応答を Dictionary<int,string> にプリロードし、Volatile.Read でロックレス取得

## 🚀 クイックスタート

```bash
dotnet run --project TcpServer --port 9001 --secret mypass
```

1. `HELLO mypass` を送信 → `OK\r\n`  
2. 任意コマンドを送信 (`PING`, `RD 3001` など)  
3. 終了時は `BYE mypass` を送信  

> ビルド要件: .NET 6 以上／シングルファイル publish 対応。  
> 実装は別リポジトリ `<coming soon>` にて公開予定です。

## 🧪 テスト活用例

- **並列リクエスト保証**：複数 `CallAsync` を同時発行し、レスポンス順序を確認  
- **タイムアウト挙動**：意図的に応答を遅延させ、`TimeoutException` 処理を検証  
- **負荷試験**：`wrk`・`bombardier` 等で 10 万 req/s 超えを想定したストレステスト  
