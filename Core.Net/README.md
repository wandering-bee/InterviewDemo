# 🧩 SledChannel — 非同期 TCP 通信モジュール

## 📝 キーワード
- TCP 全二重
- リクエスト-レスポンス
- 非同期 Channel & PipeReader
- CRLF（\r\n）フレーミング
- ゼロコピー Decode
- 高並列インフライト制御
- IPv4・IPv6 デュアルスタック
- 送信・受信タスク分離協調ループ

## 📦 モジュールの目的
SledChannel は ――  
「小容量 × 高頻度」メッセージを確実・高速に往復させるための **汎用 TCP チャネル** です。  
複数リクエストが同時進行しても、到着順に乱れなくレスポンスを対応付けられるのが最大の特長です。

## 📐 構成コンポーネントと技術ポイント

| # | コンポーネント     | 主要技術 | 役割 |
|--:|-------------------|----------|------|
| 1️⃣ | `SledLinkTcp`     | IPv4/IPv6 デュアルスタック／TcpClient.NoDelay & Keep-Alive | ソケット確立＋NetworkStream 公開 |
| 2️⃣ | `KvAsciiCodec`    | CRLF フレーム／SequenceReader ゼロコピー | "PING\r\n" ⇔ "PONG\r\n" の ASCII フレームを符号化・復号 |
| 3️⃣ | `SledChannel`     | Channel 送信キュー／SemaphoreSlim で _maxInFlight 制御<br>PipeReader 受信分割／ArrayPool<byte> 再利用 | 送信・受信・応答マッチングのハブ、FIFO で保留要求を管理 |

> 📝 補足：テスト用の仮想サーバ `TcpServer` は別 README に分離しています。

## 🔁 通信フロー（概念図）

```
[呼び出し側]  ── CallAsync() ─┐
                              ↓
        ┌─(1) 送信キューへ投入─────────────┐
        │                                   │
(3) FIFO │                         ネットワーク│(2) SendAsync
        │                                   ↓
Task 登録 & 待機                 ┌─────────────┐
        ↑                      │   リモート  │
        │           ┌── recv ──┤   デバイス  │
        └───────────┤          └─────────────┘
                    │
          PipeReader / TryDecode
                    │
         (4) ペンディング要求と照合
                    │
         TaskCompletionSource.SetResult
```

## 🔧 使用例（C#）

```csharp
var link  = new SledLinkTcp();
await link.ConnectAsync("127.0.0.1", 9001);

var codec = new KvAsciiCodec();
var chan  = new SledChannel(link, codec);

ReadOnlyMemory<byte> rsp = await chan.CallAsync(
    Encoding.ASCII.GetBytes("PING"));

Console.WriteLine(Encoding.ASCII.GetString(rsp.Span)); // → "PONG"
```

> 既定タイムアウトは 1000 ms。応答なしの場合は `TimeoutException` が発生します。

## 🚀 パフォーマンスメモ

ループバック・シングルコア環境で **1 万リクエスト** を

- **Zen 4 = 3.9 秒**  
  <div align="left">

  $$
  \frac{10\,000}{3.9\ \text{s}} = 2\,564\ \text{req/s} \;\approx\; 154\ \text{k req/min}
  $$

  </div>

- **Haswell-E = 6.2 秒**  
  <div align="left">

  $$
  \frac{10\,000}{6.2\ \text{s}} = 1\,613\ \text{req/s} \;\approx\; 97\ \text{k req/min}
  $$

  </div>

※ネットワーク遅延は含まれていません

## 🧠 まとめ

SledChannel + KvAsciiCodec でテキストプロトコル機器との統合を最小手間で実装。  
IPv4/IPv6 透過、ソケット自動チューニング。  
詳細テストには別途公開の `TcpServer` を利用可能。
