# 🧩 SledChannel — TCP通信モジュール（日本語版ドキュメント）

## 📝 キーワード

- TCP全二重通信  
- リクエスト-レスポンスモデル  
- 非同期キュー／パイプライン  
- CRLF（\r\n）フレーミング  
- 高並列・インフライト制御  
- ゼロコピー TryDecode  
- 送受信を分離したシングルスレッド協調ループ  
- IPv4 / IPv6 自動判別  

---

## 📦 モジュールの目的

SledChannel は小さなメッセージを高頻度でやり取りするアプリケーション向けに設計された **高性能な非同期 TCP チャネル** です。  
複数のリクエストを同時に発行しても、対応するレスポンスを **順序乱れなく確実に対応付け** できるのが特徴です。

---

## 📐 構成コンポーネントと技術ポイント

| # | コンポーネント | 主要技術 | 役割 |
|--|----------------|----------|------|
| 1️⃣ | SledLinkTcp | - IPv4/IPv6 デュアルスタック<br>- TcpClient.NoDelay & KeepAlive 最適化 | 基盤となる TCP ソケットを確立し、NetworkStream を公開する。 |
| 2️⃣ | KvAsciiCodec | - CRLF テキストプロトコル<br>- 受信側は SequenceReader でゼロコピー分離 | `"PING\\r\\n"` → `"PONG\\r\\n"` などの ASCII フレームを符号化・復号する。 |
| 3️⃣ | SledChannel | - Channel<T> による送信キュー<br>- SemaphoreSlim で最大同時飛行数を制御<br>- PipeReader で受信分割<br>- ArrayPool<byte> によるバッファ再利用 | 送信・受信・応答マッチングのハブ。FIFO でペンディング要求を保持し、レスポンス到着時に TaskCompletionSource を完了させる。 |
| 4️⃣ | TcpServer（仮想サーバ） | - Shift-JIS／ASCII 並行対応<br>- HELLO / BYE シークレットで簡易認証<br>- プリペアド辞書レスポンス | テスト用の軽量サーバ。3000 系 RD コマンドや PING→OK を模擬し、実運用前の動作確認に利用できる。 |

---

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

呼び出し側は Channel.Writer にリクエストバッファを投入。  
送信ループが NetworkStream.WriteAsync で一括送信。  
同時実行数は `_maxInFlight` で制御し、過負荷を防止。  
受信ループは TryDecode 完了フレームを取り出し、待機中 Task に結果を返す。

---

## 🔧 使用例（C# 擬似コード）

```csharp
var link   = new SledLinkTcp();
await link.ConnectAsync("127.0.0.1", 9001);

var codec  = new KvAsciiCodec();
var chan   = new SledChannel(link, codec);

ReadOnlyMemory<byte> rsp = await chan.CallAsync(Encoding.ASCII.GetBytes("PING"));
Console.WriteLine(Encoding.ASCII.GetString(rsp.Span));   // -> "PONG"
```

**備考：**  
CallAsync のタイムアウトは任意指定（既定 1000 ms）。  
レスポンスが得られなかった場合には `TaskCanceledException` が返され、上位で再試行やフォールバック処理が可能です。

---

## 🖥️ 仮想サーバ（TcpServer）の概要

| 項目 | 内容 |
|------|------|
| コネクション上限 | 6 |
| 文字コード | Shift-JIS（RD **** 系）／ASCII (PING, HELLO, など) |
| 認証 | `HELLO <secret>` → `OK\r\n`、`BYE <secret>` → `BYE\r\n` で切断 |
| コマンド辞書 | RD 3001～RD 6000 → `0\r\n` 、未定義は `?\r\n` |
| 実装技法 | PipeReader による CR / CRLF フレーミング、クライアントごとに CancellationTokenSource 管理 |

このサーバをローカルで起動しておけば、SledChannel の動作確認（マルチクライアント接続、並列リクエスト、タイムアウト処理など）を手軽に行えます。

---

## 🚀 パフォーマンスメモ

内部ベンチマークでは、Zen 4（3.9 s）/ Haswell-E（6.2 s）という実行時間差で **13 万リクエスト** を完走。  
測定条件はシングルコア固定・ループバック接続であり、実運用時のネットワーク遅延は含みません。

---

## 🧠 まとめ

- SledChannel は「小容量 × 高頻度」通信のために最適化された **非同期 TCP チャネル**。
- KvAsciiCodec と組み合わせることで、テキストベース機器とのインテグレーションを短時間で実装可能。
- SledLinkTcp は IPv4/IPv6 を透過的に扱い、ソケットパラメータを自動チューニング。
- TcpServer を同梱することで、開発段階の結合テスト／負荷試験をすぐに開始できる。

以上のモジュールを組み合わせることで、高信頼かつシンプルな TCP 通信スタックを最小限のコード量で構築できます。
