Fast Reconstruct 🛰️
リアルタイム点群を CPU 上で高速に三角メッシュ化し GPU へ直接アップロード する DLL

目次
プロジェクト概要

主な特長

クイックスタート

API 概要

性能と最適化の歩み

ログ & デバッグ

オプションマクロと依存ライブラリ

ライセンス

プロジェクト概要
fast_reconstruct は XYZ+RGBA 点群ストリーム を以下のパイプラインで処理します。

Delaunay 分割（CGAL／Subdiv2D）

高低差 LUT 彩色 & 法線生成

ゼロコピーで GPU へアップロード

OpenGL による対話描画

既定では CGAL Delaunay を使用し、
30 k 点 ≈ 32 ms（8 コア）を実現。
DLL は 純 C インターフェース で公開しているため、C#／Python 等から P/Invoke や FFI で呼び出せます。

主な特長
区分	内容
速度	30 k 点の再構築が ≈ 32 ms
最小インターフェース	fast_reconstruct.h だけで利用可能
並列化	OpenMP により自動スレッドスケール
メッシュ重複排除	Grid / Hash の二段構成で疎密両対応
彩色	constexpr LUT によるゼロアロケーション
軽量ログ	Emoji + RAII タイマーでオンオフ即切替
クロスプラットフォーム	Windows / Linux、C++17、CMake & Visual Studio 対応

クイックスタート
1. クローン
bash
复制
编辑
git clone --recursive https://github.com/yourname/fast_reconstruct.git
2. 主要依存
ライブラリ	バージョン	用途
CGAL	≥ 5.6	USE_CGAL 定義時に使用（推奨）
OpenCV	≥ 4.8	CGAL が無い場合の代替 Delaunay
OpenMP	任意	コンパイラ同梱で可

3. ビルド例（CMake + CGAL）
bash
复制
编辑
cmake -B build -DUSE_CGAL=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
生成物 fast_reconstruct.(dll|so) とヘッダを配布すれば OK です。

API 概要
すべて POD 構造体 + 列挙型 でエクスポートしており、安全に言語間受け渡し可能です。完全定義は fast_reconstruct.h を参照してください。

関数	説明
Err ReconstructAA(const Vec3f* pts, uint32_t cnt, ReconstructMode mode, double tol, Mesh* out, LogFn cb)	点群 → メッシュのメインエントリ
void FreeMesh(Mesh* m)	ReconstructAA が確保したバッファを解放

代表的な構造体
cpp
复制
编辑
struct Vec3f   { float x, y, z; };
struct VertexF { Vec3f pos, nor, col; };
struct Mesh    { VertexF* verts; uint32_t* idx;
                 uint32_t vCnt, iCnt; };
enum class Err { Ok, EmptyInput, AllocFail };
最小コード例
cpp
复制
编辑
#include "fast_reconstruct.h"

int main() {
    std::vector<Vec3f> pc = loadPointCloud();   // 点群を読み込み
    Mesh mesh{};
    auto err = ReconstructAA(pc.data(), pc.size(),
                             ReconstructMode::Subdiv, 0.0, &mesh);
    if (err == Err::Ok) {
        uploadToGPU(mesh.verts, mesh.idx, mesh.vCnt, mesh.iCnt);
        FreeMesh(&mesh);
    }
}
性能と最適化の歩み
バージョン	実装	点数	処理時間
初期	NetTopologySuite (C#)	30 k	≈ 380 ms
1st	OpenCvSharp4 Subdiv2D	30 k	≈ 140 ms
2nd	ネイティブ OpenCV 4.11	30 k	≈ 82 ms
現行	CGAL + 最適化	30 k	≈ 32 ms

測定環境：Intel® Core™ i7-12700H / Windows 11 / MSVC 19.39

ログ & デバッグ
LogFn コールバックを渡すだけで段階別の実行時間を出力できます。

cpp
复制
编辑
void myLog(const char* s) { std::puts(s); }

ReconstructAA(pc, n, ReconstructMode::Subdiv, 0, &mesh, myLog);
/* 出力例
🔄 ReconstructAA: begin
📦 use flat grid
🔄 Delaunay       14.21 ms
✅ ReconstructAA: ok            32.47 ms
*/
オプションマクロと依存ライブラリ
マクロ	効果	追加依存
USE_CGAL	CGAL Delaunay を使用（最速）	CGAL ≥ 5.6
_OPENMP	並列法線計算を有効化	OpenMP
FR_DLL_STATIC	静的ライブラリを生成	なし

ライセンス
現在ライセンスは未設定のため 商用利用不可 とします。
商用利用を希望される場合は作者までお問い合わせください。
