> 💠 面接用デモ

[![License: CC BY-NC-ND 4.0](https://img.shields.io/badge/License-CC%20BY--NC--ND%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-nd/4.0/)

# ⚖️ オリジナル声明（Legal Notice）

本プロジェクト InterviewDemo は、作者が勤務時間外に個人的に設計・実装したオリジナル作品です。

現在または過去の雇用主から提供されたコード、資料、内部ドキュメント、専有情報は一切含まれていません。

また、本プロジェクトの内部実装には 未公開・クローズドソースのプライベートリポジトリ で開発されている独自アーキテクチャ 「Arclith」 を基盤としたラッパー／フレームワークが組み込まれています。

現時点で一般公開の予定もありません。

本プロジェクトは、在職中または過去に所属していた企業が使用・展開・開発した商用プロジェクトとは無関係であり、商用環境での利用実績もありません。

本リポジトリの内容は 個人ポートフォリオ、学習・研究、および技術交流 のみを目的としています。
商用利用や大規模な引用を希望される場合は、必ず作者までご連絡のうえ、事前に許諾を取得してください。


# 📦 InterviewDemo ― 点群転送・可視化のエンドツーエンドデモ

🕓 **最終更新日**：2025年5月8日  
🧑‍💻 **作者**：wandering-bee

---

## 🔍 GUI

![Demo UI](https://raw.githubusercontent.com/wandering-bee/InterviewDemo/main/img/Main.png)

---

---

## 🚀 プロジェクト概要

本リポジトリは、点群生成 → 高速再構成 → GPU描画 → 通信負荷試験までを複数プロセスで連携させた、WinUI3 中心制御型の統合デモアーキテクチャです。
WinUI3 アプリが中心となり、3D ビジュアライザと通信サーバを起動・連携。それぞれ以下のような役割を担います：

- 軽量・シンプルな構成  
- フレームベース送信  
- 局所的なアルゴリズムを用いた構造解析
- 面談・ポートフォリオ用途に最適化済み

> 🧪 本プロジェクトは ViridisNet から抽出された設計パターンを簡素化・再構成した技術検証用サブセットです。

---

## 🧩 モジュール構成

| モジュール名              | 種別              | 役割                                      |
|---------------------------|-------------------|-------------------------------------------|
| `Demo.Showcase.App`       | WinUI3アプリ       | 点群描画クライアント                       |
| `Core.VGV`                | WinForms + 3D     | 点群生成・再構成・3D表示・Pipe通信          |
| `Core.Net`                | .NET ライブラリ    | TCP/IP 通信の抽象化（接続・フレーム化など）  |
| `Core.Server`             | .NET EXE(Console) | 通信応答専用サーバ（負荷テスト用途）         |
| `CaptureTrataitsDll`      | C++ DLL           | Core.VGV 内で呼び出される高速再構成エンジン  |

---

## 🧠 技術ポイント

- 🧠 WinUI3 × Named Pipe 通信
中心アプリから各プロセスを起動・制御し、状態を双方向同期

- 🧩 点群生成・3D再構成
Core.VGV にて DLL 経由で点群を生成・再構成し、GPU により描画

- 📶 TCP通信負荷テストサーバ
Core.Serverは複数クライアントからの同時接続に対応し、受信した要求に対して応答のみを行います。

- 🔌 通信抽象ライブラリ Core.Net
Connect/Frame/SendLoop 等の通信機構を統一インタフェースとして提供

---

## 🧩 システム構成

本デモは、WinUI3アプリ（Demo.Showcase.App）と、それが起動する2つの外部プロセスで構成されます。

```
[Demo.Showcase.App]
    ├─ 起動 → Core.VGV.exe（点群生成・再構成・3D描画）
    │        ⇄ Named Pipe による状態・指令の同期
    └─ 起動 → Core.Server.exe（通信ストレステスト用サーバ）
```

## 🔁 処理フロー（Core.VGV）
```
Core.VGV.exe
    ⇓ アルゴリズムを用いてリアルタイム 3D 表示
    ⇓ CaptureTrataitsDll 経由でデータを生成
    ⇓ CGAL ベースのアルゴリズムにより構造を再構成
```

> 🧠 Core.VGV は視覚化処理の中心となるモジュールであり、外部 DLL（CaptureTrataitsDll）からの点群取得と、GPU による高速描画を担います。構造再構成には CGAL を使用し、高密度なジオメトリ処理にも対応しています。

## 📊 性能指標

| ステージ                | 処理対象       | 処理時間（参考） |
|------------------------|----------------|------------------|
| 三角形分割（Delaunay）  | 約3万頂点       | 約32ms           |
| 法線＋LUTカラー変換     | 約3万頂点       | 約2ms            |
| GPU転送                | -              | 未計測            |
| 描画（制限なし）        | -              | FPS制限なし       |

### 🔧 最適化の履歴

- 初期実装（NetTopologySuite）: 約380ms  
- 第一段階（OpenCvSharp4）: 約140ms  
- 第二段階（OpenCV 4.11 DLL）: 約82ms  
- 現在（CGAL 実装）: **約32ms**

---

## 🧪 実行方法

### CaptureTrataitsDll ― ビルド & 最適化設定

🔧 本 DLL は Core.VGV との連携用に設計された C++ ネイティブライブラリであり、高速な点群処理を目的としています。

### ビルド設定（Release | x64）

| カテゴリ             | 設定内容                                 |
|----------------------|------------------------------------------|
| 最適化               | /O2 速度最優先                           |
| 警告レベル           | 警告レベル /W4（厳格な警告レベル）          |
| ランタイム           | /MD - マルチスレッド DLL                 |
| 関数レベルリンク     | /Gy（関数単位でのリンク）                |
| リンク時コード生成   | /LTCG 有効                               |
| SIMD 拡張命令        | /arch:AVX2                               |
| 並列化               | OpenMP 有効（/openmp）                   |
| 使用ライブラリ       | OpenCV 4.11 / CGAL + Boost・GMP・MPFR   |
| プリコンパイルヘッダ | 不使用                                   |
| ビルド構成           | Release                                   |

### 実行時の注意

生成された DLL および PDB ファイルは、**Core.VGV/bin/Release/net8.0/** へ手動でコピーしてください。パスが通らない場合、実行時に `DllNotFoundException` が発生します。

### 関連モジュールとビルドコマンド

### Core.Net
```
dotnet build Core.Net/Core.Net.csproj -c Release
```
依存パッケージ：
- System.Threading.Channels
- System.IO.Pipelines（.NET 8 同梱）

### Core.Server
```
dotnet build Core.Server/Core.Server.csproj -c Release
```
依存パッケージ：
- System.IO.Pipelines
- Spectre.Console

### Core.VGV
```
dotnet build Core.VGV/Core.VGV.csproj -c Release
```
依存パッケージ：

| カテゴリ       | パッケージ名                                 |
|----------------|----------------------------------------------|
| 点群・幾何処理 | NetTopologySuite 2.6.0                        |
| 画像処理       | OpenCvSharp4 系列 (4.10.0.*)                 |
| GPU 描画       | OpenTK, OpenTK.GLControl                     |
| ログ出力       | Serilog + Serilog.Sinks.Console 他          |
| 画像読込       | StbImageSharp 2.30.15                        |

### Demo.Showcase.App
```
dotnet build Demo.Showcase.App/Demo.Showcase.App.csproj -c Release
dotnet run --project Demo.Showcase.App -c Release
```

追加パッケージなし。起動時に Core.Server / Core.VGV を自動で起動・連携します。

## 推奨ビルド順
※ DLL を最初にビルドし、Core.VGV/bin/... へコピー
```
CaptureTrataitsDll → Core.Net → Core.Server → Core.VGV → Demo.Showcase.App
```

---

## 🧰 使用目的・活用シーン

- 📺 **技術ポートフォリオ提示**  
  構成力・性能意識・モジュール性のアピール

- 🔬 **軽量アルゴリズム評価**  
  プログラミング技術の比較と評価

- 🧩 **アーキテクチャ設計のテンプレート**  
  通信 → 処理 → 表示 の標準構成提示

- 🎓 **教育・研修用教材**  
  点群処理・Socket通信・描画の統合例

---

## 📘 補足事項

- 各モジュール直下の `README.md` はテンプレート状態です。必要に応じて機能・インタフェースごとに記述を追加してください。
- 通信はすべて CRLF（`\r\n`）区切りの簡易フレーミングを採用しています。
- `.vsconfig` や `launchSettings.json` などの開発支援ファイルは必要に応じて追加可能です。
