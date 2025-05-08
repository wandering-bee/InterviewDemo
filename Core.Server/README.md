# Sled TCP Server 🚦

Shift-JIS ベースのコマンドを扱う 軽量・コンカレント TCP サーバー

---

## 📖 目次

- [概要](#概要)  
- [主な機能](#主な機能)  
- [ビルド & 実行](#ビルド--実行)  
- [プロトコル仕様](#プロトコル仕様)  
- [ログ出力](#ログ出力)  
- [終了 & クリーンアップ](#終了--クリーンアップ)  
- [拡張ポイント](#拡張ポイント)  
- [ライセンス](#ライセンス)  

---

## 📝 概要

エントリーポイント `Program.cs` は CLI で渡された `--port` と `--secret` を読み取り、  
`TcpServer` を非同期で起動します。最大 **6 クライアント** まで同時接続でき、  
**Shift-JIS / CRLF 終端**のコマンドを高速にパースして返信します。

---

## 🚀 主な機能

| 区分         | 内容 |
|--------------|------|
| 同時接続数     | 最大 6 クライアント ― 超過時は即座に拒否 |
| 認証         | `HELLO <secret>` / `BYE <secret>` ハンドシェイク；シークレット不一致時は `ERR` 応答 |
| 辞書応答     | `RD 3001`〜`RD 6000` → `0\r\n`；`PING` → `OK\r\n`；未定義 → `?\r\n` |
| エンコーディング | 受信・送信ともに Shift-JIS；内部は `ReadOnlyMemory<byte>` でゼロコピー |
| ログ         | Spectre.Console によるカラータグ（[Info], [Warn] など） |
| 安全終了     | 標準入力へ `EXIT <procSecret>` を流すと全ソケットを安全にクローズし終了 |

---

## ⚙️ ビルド & 実行

.NET 8.0 以降を前提にしています。必要に応じて `Core.Server.csproj` の `TargetFramework` を調整してください。

```bash
# 依存取得（Spectre.Console）
dotnet restore

# リリースビルド
dotnet publish -c Release -o out

# 実行例（ポート 15000, プロセス用シークレットを変更）
./out/Sled.Server --port 15000 --secret ABCD1234
```

| オプション | 既定値 | 説明 |
|------------|--------|------|
| `--port`   | 12006  | 待受ポート番号 |
| `--secret` | SLED-LOCAL-DEV-PROC | 終了ハンドシェイク用シークレット |

引数解析は `ArgsExtensions.GetOption` により実装されています。

---

## 📡 プロトコル仕様

```
# 文字コード : Shift-JIS
# 区切り     : CR または CRLF

1. 認証
   クライアント → サーバー : "HELLO <secret>\r\n"
   サーバー   → クライアント : "OK\r\n" (成功) / "ERR\r\n" (失敗)

2. 通常コマンド
   ・"RD 3001"〜"RD 6000" : 常に "0\r\n" を返却
   ・"PING"               : "OK\r\n" を返却
   ・その他               : "?\r\n" を返却

3. 切断
   クライアント → サーバー : "BYE <secret>\r\n"
   サーバー   → クライアント : "BYE\r\n" → ソケットクローズ
```

受信バッファは `System.IO.Pipelines` を用い、CR(LF) でフレーム分割しています。  
コマンド辞書は `Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>` により構成され、`SequenceEqual` ベースで高速検索します。

---

## 🖨️ ログ出力

| レベル   | 例                               | 用途     |
|----------|----------------------------------|----------|
| Info     | `[Info] Listening on 0.0.0.0:12006` | 一般情報 |
| Success  | `[Success] Client connected.`    | 正常完了 |
| Warn     | `[Warn] Connection refused`      | 軽微な問題 |
| Error    | `[Error] Unhandled exception`    | 重大エラー |
| Recv     | `[Recv] HELLO …`                 | 受信データ |
| Send     | `[Send] OK`                      | 送信データ |

`Spectre.Console` のマークアップにより、ターミナルで色分け表示されます。

---

## 🧹 終了 & クリーンアップ

- **Ctrl-C**：`Console.CancelKeyPress` ハンドラで `CancellationToken` を発火し安全終了  
- **親プロセス連携**：STDIN へ `EXIT <procSecret>` を書き込むと同様に全リソースを解放して終了  

`TcpServer.DisposeAsync()` はすべてのクライアント `CancellationTokenSource` をキャンセルし、ソケットを確実に閉じます。

---

## 🔧 拡張ポイント

| 項目               | 方法 |
|--------------------|------|
| コマンド辞書を増やす | `BuildDict()` に追加ロジックを記述 |
| 同時接続数を増減     | `TcpServer._maxConnections` を変更 |
| 認証アルゴリズム     | `HandleClientAsync()` 内の HELLO / BYE 解析を差し替え |
| ログフォーマット     | `Logger` クラスの各メソッドをカスタマイズ |

---

## 📜 ライセンス

現時点では未設定（**非商用・社内利用限定**）  
**商用ライセンスが必要な場合はリポジトリ管理者までご連絡ください。**
