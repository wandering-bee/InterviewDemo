
# Fast Reconstruct 🛰️
リアルタイム点群 → メッシュ再構築 DLL

## 📡 プロジェクト概要
Fast Reconstruct は XYZ 点群を CPU 上で 2-D Delaunay 分割 → 法線 & ターボ LUT 彩色 → メッシュ に変換し、  
結果を 純 C インターフェース でエクスポートする軽量 DLL です。

- CGAL または OpenCV Subdiv2D をコンパイル時に切替え  
- ハッシュ／フラットグリッドで重複点除去  
- OpenMP 対応の並列法線計算  
- Tiny RAII ロガーで段階別タイミングを計測  

## 🛠️ 依存 & ビルド

| 区分       | 必須 / 任意 | バージョン目安 | 用途               |
|------------|--------------|----------------|--------------------|
| OpenCV     | 必須 (デフォルト) | ≥ 4.8         | Subdiv2D Delaunay |
| CGAL       | USE_CGAL 定義時   | ≥ 5.6         | 高速 Delaunay (推奨) |
| OpenMP     | _OPENMP 定義時   | -             | 並列法線 & LUT 生成 |
| C++17      | 必須           | -             | 言語機能            |

### CMake ビルド例

```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release -DUSE_CGAL=ON
cmake --build build --config Release
```

生成物 `fast_reconstruct.dll / fast_reconstruct.lib / fast_reconstruct.h` を  
消費側プロジェクトに配置してください。  
Windows 以外では `.so` を生成します。

## 🚀 パイプライン概要

- パス0: 1 pass で AABB を取得  
- パス1: 点密度を判定し  
  - 高密度 ⇒ フラットグリッド  
  - 疎 ⇒ ハッシュグリッド  
- パス2: 2-D Delaunay 分割 (CGAL / OpenCV)  
- パス3: (x,y)→元インデックス解決  
- パス4-7:  
  - LUT 彩色（Turbo 1275 色）  
  - 法線累積（OpenMP 対応）  
  - 正規化  
- パス8: Mesh 構造体へ詰替え & 返却  

## 🧩 API リファレンス

### 主要型

```cpp
struct Vec3f  { float x, y, z; };
struct VertexF { Vec3f pos, nor, col; };
struct Mesh   { VertexF* verts; uint32_t* idx;
                uint32_t vCnt,  iCnt; };

enum class ReconstructMode : uint32_t { Subdiv = 0, Topology = 1 };
enum class Err : uint32_t { Ok, EmptyInput, AllocFail };
using  LogFn = void(*)(const char*);         // 可 nullptr
```

### エクスポート関数

| 関数 | 説明 |
|------|------|
| `Err ReconstructAA(const Vec3f* pts, uint32_t cnt, ReconstructMode mode, double tol, Mesh* out, LogFn cb=nullptr)` | 点群 ➜ メッシュ主関数 |
| `void FreeMesh(Mesh* m)` | `ReconstructAA` が確保したバッファ解放 |

### 最小使用例

```cpp
#include "fast_reconstruct.h"
int main() {
    std::vector<Vec3f> cloud = loadPCD();
    Mesh mesh{};
    if (ReconstructAA(cloud.data(), cloud.size(),
                      ReconstructMode::Subdiv, 0.0, &mesh) == Err::Ok) {
        upload(mesh.verts, mesh.idx, mesh.vCnt, mesh.iCnt);
        FreeMesh(&mesh);
    }
}
```

## 📝 ログ & デバッグ

LogFn を渡すと、Emoji + 計測付きログが得られます。

```yaml
🔄 ReconstructAA: begin
📦 use flat grid
🔄 Delaunay     32.45 ms
✅ ReconstructAA: ok        35 ms
```

## ⏱️ 性能スナップショット

| 実装               | 点数    | 処理時間*    |
|--------------------|---------|---------------|
| CGAL(Release/O2, AVX2) | 30 k | ≈ 32 ms       |
| OpenCV Subdiv2D     | 30 k   | ≈ 82 ms       |

## 📐 性能換算

<div align="left">

$$
\frac{30\,000}{32/1000} = 937\,500 \ \text{pts/s} \approx 0.94\ \text{M pts/s}
$$

</div>

\* i7-5820k / Win 11 / MSVC 19.39・OpenMP ON 時計測。

## ⚙️ ビルドオプション

| マクロ        | 効果              | 追加依存      |
|---------------|-------------------|---------------|
| USE_CGAL      | CGAL Delaunay 使用 | CGAL ≥ 5.6    |
| _OPENMP       | 並列化有効化       | OpenMP        |
| FR_DLL_STATIC | 静的ライブラリ生成 | なし          |

## 🖇️ ライセンス

現在ライセンス未設定。  
商用・再配布を希望される場合は作者までご連絡ください。

## 備考

DLL ビルド後に `.def` ファイルでエクスポート順序を固定しています。  
Win32 では `__declspec(dllexport)`、他プラットフォームは `extern "C"` で公開。
