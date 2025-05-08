# 🎥 3D ビジュアライゼーション・モジュール「一枚流し」設計概要 v2（日本語版）

🕓 **最終更新**：2025‑05‑08

---

## 1. 🎯 目的

リアルタイムに受信する点群ストリーム (XYZ + RGBA) を――

- CPU 上で三角メッシュへ再構築（Delaunay／Subdiv2D）  
- 高低差に応じた LUT 彩色 & 法線生成  
- GPU へゼロコピーでアップロード  
- OpenGL で対話的に描画  

までをワンストップで実現する。

**タグライン**：『点群 → メッシュ → 描画』を〈一枚流し〉＋〈ホットリロード〉で。

---

## 2. 🔁 入出力

| 内容   | 入力                       | 出力                                    |
|--------|----------------------------|-----------------------------------------|
| 点群可視化 | 点群 XYZ(+RGBA) ストリーム | sRGB カラー三角メッシュ + OpenGL ウィンドウ |

---

## 3. 🆕 今バージョン（v2）での主な追加機能

- **Shader ホットスワップ**：GLSL を ProgramBinary キャッシュから即時更新  
- **視錐台カリング**：CPU 側 AABB による描画最適化  
- **sRGB / 異方性フィルタ対応**：色再現性とテクスチャ品質向上  
- **Instancing 準備**：VertexArrayBindingDivisor による per-instance 属性対応

---

## 4. 🧩 全体アーキテクチャ

```
点群入力
  └─▶ ❶ Reconstruct Core (C++ fast_reconstruct)          
        └─▶ ❷ Managed Bridge (CaptureTrataits P/Invoke)  
              └─▶ ❸ GPU Resource Layer                 
                    ├─ GPUStreamBuffer<T> (永続マップ RingBuffer)
                    └─ VertexArrayObject<T> (DSA & 自動レイアウト)
                        └─▶ ❹ Rendering Pipeline (ViewEngine)
                              └─▶ ❺ Interaction (GLInteractor)
```

---

## 4.1 🧱 サブシステム詳細

| #   | サブシステム       | 主要コード                  | ハイライト |
|-----|--------------------|-----------------------------|------------|
| ❶  | Reconstruct Core   | fast_reconstruct.cpp        | OpenCV Subdiv2D / CGAL、SIMD + OpenMP、LUT 彩色 |
| ❷  | Managed Bridge     | CaptureTrataits.cs          | P/Invoke によるゼロコピー転送 |
| ❸  | GPU Resource       | GPUStreamBuffer<T> 他       | 永続マップ & フェンス同期による高速転送 |
| ❸  | GPU Layout         | VertexArrayObject<T>        | 反射キャッシュ + 自動属性設定 |
| ❹  | Render Engine      | ViewEngine.cs, Shader.cs    | frustum カリング, sRGB, instancing |
| ❺  | Interactor         | GLInteractor.cs             | マウス操作（Orbit, Pan, Zoom, Reset）|

---

## 5. 📈 データフロー（詳細）

```
点群 (XYZ(+RGBA))
  ↓   (P/Invoke)
[ ReconstructAA ]  — C++ —
  ↓   (VertexF[] + uint[])
[ GLMesh ]
  ↓   (GPUStreamBuffer "リング")
[ VBO / EBO ]
  ↓
[ VAO 自動構成 ]
  ↓
[ Draw Arrays / Elements ]
```

---

## 6. ✨ 技術ハイライト

- **Delaunay 再構築**（Subdiv2D / CGAL）  
  - XY 平面上の 2D Delaunay、Subdivision 切替可能

- **高低差カラーリング**  
  - LUT 256 エントリ、Z を正規化して着色

- **SIMD + OpenMP 法線合算**  
  - AVX2 による 4×double 並列、チャンク処理＋原子統合

- **GPU Permanent-Mapped RingBuffer**  
  - GL_MAP_PERSISTENT_BIT | GL_MAP_COHERENT_BIT、glFenceSync 使用

- **VAO 自動レイアウト**  
  - 型情報キャッシュで O(1) 属性設定、Matrix4→vec4 分解

- **Shader ホットスワップ**  
  - 保存後1フレームで再リンク、ProgramBinary キャッシュ利用

- **視錐台カリング**  
  - CPU 側で画面外メッシュスキップ

- **描画品質**  
  - MSAA + GL_SAMPLE_ALPHA_TO_COVERAGE、sRGB、異方性フィルタ

---

## 7. 🔌 C++ / C# インターフェイス

### C++ 側

```cpp
struct Mesh {
    VertexF* verts;
    uint32_t* idx;
    uint32_t vCnt;
    uint32_t iCnt;
};

extern "C" Err ReconstructAA(const Vec3f* pc, uint32_t n,
                             ReconstructMode mode, double tol,
                             Mesh* outMesh, LogFn log);

extern "C" void FreeMesh(Mesh* m);
```

### C# 側

```csharp
[DllImport("CaptureTrataitsDll.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern Err ReconstructAA(...);
```

---

## 8. 📊 性能指標（参考：i7‑5820K + RTX 3080Ti）

| ステージ                      | 処理量             | 処理時間                  |
|------------------------------|--------------------|---------------------------|
| 点群 → Delaunay（三角形分割） | 約 3 万頂点        | ≈ 32 ms                   |
| 法線計算 + LUT ベース彩色処理 | 約 3 万頂点        | ≈ 2 ms                    |
| GPU へアップロード           | -                  | 測定なし                  |
| フレーム描画（制限なし）     | -                  | フレームレート未固定（制限なし） |

### 🛠 開発ステップと最適化の経緯：

- **初期バージョン**：  
  NetTopologySuite による処理で、約 380ms を要していた。実装は簡易だが、性能面でボトルネックに。

- **第一回最適化**：  
  OpenCvSharp4 経由で Subdiv2D を使用し、140ms まで短縮。だが C# ラッパーのオーバーヘッドが気になる。

- **第二回最適化**：  
  原生 OpenCV 4.11（C++ DLL 直叩き）を導入し、82ms に到達。性能は改善するも、精度や柔軟性に限界。

- **現行バージョン**：  
  CGAL による Delaunay 分割を採用し、ついに32msへ。品質・安定性ともに現時点で最良。

---

## 9. 📌 今後の拡張予定

- Meshlet / Cluster Culling（GPU）  
- Compute Shader ベース再構築  
- PBR マテリアル + IBL 対応  
- マルチフレーム TAA 実装  

---

© 2025 Axone Engineering
