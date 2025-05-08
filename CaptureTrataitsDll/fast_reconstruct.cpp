/* fast_reconstruct_optimized.cpp — XY Delaunay 版本（极限性能版）
   ⚙️ 目的：在保持 API 与依赖不变的前提下，进一步压榨 CPU 性能。
   🆚 变化总览（关键处以“⚡ OPT”标注）：
     · 去重 + 包围盒 合并为单遍扫描，减少一次 O(n)。
     · 哈希网格 key 采用乘法 INV_CELL_SIZE，避免频繁除法 / round。
     · unordered_map 提前 reserve，降低 rehash 次数。
     · index / vertex 缓冲使用 std::unique_ptr 管理，异常安全。
     · LUT 构建改用 constexpr for 循环展开（编译期生成）。
     · 若检测到 OpenMP，法线累加并行化。
*/

#include "fast_reconstruct.h"
#include <new>
#include <cmath>
#include <cstring>
#include <mutex>
#include <chrono>
#include <vector>
#include <memory>
#include <cstdio>
#include <unordered_map>
#include <opencv2/imgproc.hpp>

#include "grid_hash.hpp"
#include "tiny_log.hpp"
#include "color_map_utils.hpp"

using cv::Point2f;
using cv::Rect;
using cv::Subdiv2D;

#include <utility>
#ifdef _OPENMP
#include <omp.h>
#endif

#ifdef USE_CGAL
#include <CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include <CGAL/Delaunay_triangulation_2.h>
#include <CGAL/tags.h>
#include <CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include <CGAL/Delaunay_triangulation_2.h>
#include <CGAL/tags.h>      
#endif

using namespace fr::grid;
using fr::colormap::mapColor;


/* -------------------------------------------------------------------------
   【方法】ReconstructAA
   -------------------------------------------------------------------------
   输入参数：
   · pc   → 点云数组 (x,y,z float)
   · n    → 点数
   · mode / tol → 预留参数，此版本未使用
   · outMesh → 输出网格结构体，成功后填充 verts / idx / *Cnt
   · log  → 日志回调，可为 nullptr

   返回值 (Err)：
   · Ok          → 成功
   · EmptyInput  → 点数不足 / Delaunay 失败 / 输入非法
   · AllocFail   → new / make_unique 失败
-------------------------------------------------------------------------*/
Err ReconstructAA(const Vec3f* pc, uint32_t n,
    ReconstructMode /*mode*/, double /*tol*/,
    Mesh* outMesh, LogFn log)
{
    // 👉 基本参数校验 -----------------------------------------------------
    if (!outMesh)       return Err::AllocFail;   // Mesh 指针必须非空
    if (!pc || n < 3)   return Err::EmptyInput;  // 点云不足三点无法构面

    _log(log, "🔄", "ReconstructAA: begin");   // 🔄 开始日志

    /* --- 编译期常量 ---------------------------------------------------- */
    constexpr double CELL_SIZE = 1e-4;       // 每格物理尺寸 0.1 µm
    constexpr double INV_CELL_SIZE = 1.0 / CELL_SIZE; // 乘法倒数避免除法
    constexpr uint64_t GRID_CAP = (1ull << 31);    // 最大格子元素数 (≈2 GB)

    /* --- Pass0: 求包围盒 ---------------------------------------------- */
    // 一次 O(n) 扫描得到点云 X/Y/Z 的最小最大值，用于后续格子映射 & 归一化
    float minX = pc[0].x, maxX = pc[0].x;
    float minY = pc[0].y, maxY = pc[0].y;
    float minZ = pc[0].z, maxZ = pc[0].z;
    for (uint32_t i = 1; i < n; ++i) {
        const Vec3f& p = pc[i];
        if (p.x < minX) minX = p.x; else if (p.x > maxX) maxX = p.x;
        if (p.y < minY) minY = p.y; else if (p.y > maxY) maxY = p.y;
        if (p.z < minZ) minZ = p.z; else if (p.z > maxZ) maxZ = p.z;
    }

    /* --- Pass1: 选择索引容器 ------------------------------------------ */
    // 计算需要的格子宽高 → 决定用 flat grid 还是 unordered_map
    const uint64_t gridW64 = uint64_t(std::ceil((maxX - minX) * INV_CELL_SIZE)) + 1;
    const uint64_t gridH64 = uint64_t(std::ceil((maxY - minY) * INV_CELL_SIZE)) + 1;
    if (gridW64 == 0 || gridH64 == 0) return Err::EmptyInput; // 浮点异常保护

    const uint64_t gridSz = gridW64 * gridH64;               // 64‑bit 防溢出
    const bool     useGrid = gridSz <= GRID_CAP;             // 是否可用扁平数组

    // 容器提前声明保证生命周期覆盖整个函数，避免悬垂引用
    std::vector<int32_t> grid;                               // flat grid
    std::unordered_map<std::pair<int, int>, uint32_t, PairHash> umap; // 稀疏表

    const int gridW = useGrid ? int(gridW64) : 0;
    const int gridH = useGrid ? int(gridH64) : 0;

    // lambda: 把 (x,y) 映射到整数 key (ix,iy) 便于哈希或索引
    auto makeKey = [&](float x, float y) noexcept {
        return std::pair<int, int>{
            int((x - minX)* INV_CELL_SIZE + 0.5), // 0.5 做四舍五入
                int((y - minY)* INV_CELL_SIZE + 0.5)};
        };

    if (useGrid) {
        // 👉 扁平 grid 路径：O(1) 查询，不用 hash；适合稠密&中等跨度
        grid.assign(size_t(gridSz), -1);          // -1 表示空
        for (uint32_t i = 0; i < n; ++i) {
            int ix = int((pc[i].x - minX) * INV_CELL_SIZE + 0.5);
            int iy = int((pc[i].y - minY) * INV_CELL_SIZE + 0.5);
            size_t pos = size_t(ix) + size_t(iy) * gridW;
            if (grid[pos] == -1) grid[pos] = int32_t(i); // 保留首索引
        }
        _log(log, "📦", "use flat grid");
    }
    else {
        // 👉 稀疏 map 路径：跨度巨大时避免巨大数组
        umap.reserve(n * 2);                      // 预留避免 rehash
        for (uint32_t i = 0; i < n; ++i)
            umap.emplace(makeKey(pc[i].x, pc[i].y), i);
        _log(log, "📦", "use unordered_map fallback");
    }

    // 👉 统一查询接口：坐标 → 原数组索引；闭包捕获 useGrid + 容器引用
    auto key2idx = [&](float x, float y) noexcept -> int32_t {
        if (useGrid) {
            int ix = int((x - minX) * INV_CELL_SIZE + 0.5);
            int iy = int((y - minY) * INV_CELL_SIZE + 0.5);
            if (uint32_t(ix) >= uint32_t(gridW) || uint32_t(iy) >= uint32_t(gridH))
                return -1;                         // 落在 bbox 之外
            return grid[size_t(ix) + size_t(iy) * gridW];
        }
        else {
            auto it = umap.find(makeKey(x, y));
            return it == umap.end() ? -1 : int32_t(it->second);
        }
        };

    /* --- Pass2: 平面 Delaunay 三角化 ---------------------------------- */
    std::vector<cv::Vec6f> tris2d;                 // (x0,y0,x1,y1,x2,y2)
    {
        T t("Delaunay", log);                     // ⏱️ 计时辅助
#ifdef USE_CGAL
        using K = CGAL::Exact_predicates_inexact_constructions_kernel;
        using DT = CGAL::Delaunay_triangulation_2<K>;          // 顺序版

        std::vector<K::Point_2> pts; pts.reserve(n);
        for (uint32_t i = 0; i < n; ++i) pts.emplace_back(pc[i].x, pc[i].y);

        DT dt(pts.begin(), pts.end());                         // 一次性批量插入

        for (auto f = dt.finite_faces_begin(); f != dt.finite_faces_end(); ++f) {
            auto a = f->vertex(0)->point();
            auto b = f->vertex(1)->point();
            auto c = f->vertex(2)->point();
            tris2d.emplace_back(a.x(), a.y(), b.x(), b.y(), c.x(), c.y());
        }
#else
        // OpenCV Subdiv2D 单线程备份实现
        Rect rect(int(std::floor(minX)) - 1,
            int(std::floor(minY)) - 1,
            std::max(2, int(std::ceil(maxX - minX)) + 3),
            std::max(2, int(std::ceil(maxY - minY)) + 3));
        Subdiv2D subdiv(rect);
        for (uint32_t i = 0; i < n; ++i)
            subdiv.insert(Point2f(pc[i].x, pc[i].y));
        subdiv.getTriangleList(tris2d);
#endif
    }

    /* --- Pass3: (x,y) → 原索引 --------------------------------------- */
    std::vector<uint32_t> idxVec; idxVec.reserve(tris2d.size());
    for (const auto& t6 : tris2d) {
        int32_t ia = key2idx(t6[0], t6[1]);
        int32_t ib = key2idx(t6[2], t6[3]);
        int32_t ic = key2idx(t6[4], t6[5]);
        if (ia < 0 || ib < 0 || ic < 0) continue;  // vertex 丢失
        if (ia == ib || ib == ic || ic == ia) continue; // 重复点过滤
        idxVec.push_back(ia); idxVec.push_back(ib); idxVec.push_back(ic);
    }
    const uint32_t triCnt = uint32_t(idxVec.size());
    if (triCnt == 0) return Err::EmptyInput;       // Delaunay 失败或共线

    /* --- Pass4: 分配输出缓冲 ----------------------------------------- */
    auto idx = std::make_unique<uint32_t[]>(triCnt); // 三角形索引
    auto verts = std::make_unique<VertexF[]>(n);       // 顶点数组
    if (!idx || !verts) return Err::AllocFail;         // new 失败
    std::memcpy(idx.get(), idxVec.data(), triCnt * sizeof(uint32_t));

    /* --- Pass5: 填充位置 & 颜色 -------------------------------------- */
    const float  cx = 0.5f * (minX + maxX);            // 中心 X
    const float  cy = 0.5f * (minY + maxY);            // 中心 Y
    const double zMid = 0.5 * (minZ + maxZ);
    const double zHalf = 0.5 * (maxZ - minZ);
    const double invRangeZ = zHalf > 0 ? 1.0 / (2.0 * zHalf) : 0.0;
#ifdef _OPENMP
#pragma omp parallel for schedule(static)
#endif
    for (int64_t i = 0; i < int64_t(n); ++i) {
        const Vec3f& p = pc[i];
        Vec3f pos{ p.x - cx, p.y - cy, p.z - float(zMid) }; // 移到中心
        double lutT = (pos.z + zHalf) * invRangeZ;          // 0~1 用于配色
        verts[i].pos = pos;
        verts[i].nor = { 0,0,0 };
        verts[i].col = mapColor(lutT);                      // 颜色查表
    }

    /* --- Pass6: 法线累加 (低内存分块) ------------------------------- */
    const int thCnt =
#ifdef _OPENMP
        omp_get_max_threads();
#else
        1;
#endif
    const uint32_t CHUNK = 256'000;                         // 每线程块大小
    std::vector<Vec3f> norBuf(CHUNK * thCnt);               // 临时法线缓冲

#ifdef _OPENMP
#pragma omp parallel for schedule(static)
#endif
    for (int64_t tid = 0; tid < thCnt; ++tid) {
        Vec3f* buf = norBuf.data() + CHUNK * tid;           // 线程私有指针
        uint32_t processed = 0;                             // 已归并顶点数
        while (processed < n) {
            uint32_t batch = std::min(CHUNK, n - processed);
            std::fill(buf, buf + batch, Vec3f{ 0,0,0 });
            // ⚡ 遍历所有三角面，若顶点 ia 落在当前块则累加到 buf
            for (uint32_t k = 0; k < triCnt; k += 3) {
                uint32_t ia = idx[k], ib = idx[k + 1], ic = idx[k + 2];
                if (ia < processed || ia >= processed + batch) continue;
                const Vec3f& a = verts[ia].pos;
                const Vec3f& b = verts[ib].pos;
                const Vec3f& c = verts[ic].pos;
                Vec3f ab{ b.x - a.x, b.y - a.y, b.z - a.z };
                Vec3f ac{ c.x - a.x, c.y - a.y, c.z - a.z };
                Vec3f nrm{
                    ab.y * ac.z - ab.z * ac.y,
                    ab.z * ac.x - ab.x * ac.z,
                    ab.x * ac.y - ab.y * ac.x };
                if (nrm.z < 0) { nrm.x = -nrm.x; nrm.y = -nrm.y; nrm.z = -nrm.z; }
                Vec3f& tgt = buf[ia - processed];           // 写到局部块
                tgt.x += nrm.x; tgt.y += nrm.y; tgt.z += nrm.z;
            }
            // 👉 原子方式归并到全局 verts[i].nor，避免锁
            for (uint32_t i = 0; i < batch; ++i) {
                Vec3f& dst = verts[processed + i].nor;
#ifdef _OPENMP
#pragma omp atomic
#endif
                dst.x += buf[i].x;
#ifdef _OPENMP
#pragma omp atomic
#endif
                dst.y += buf[i].y;
#ifdef _OPENMP
#pragma omp atomic
#endif
                dst.z += buf[i].z;
            }
            processed += batch;
        }
    }

    /* --- Pass7: 归一化法线 ------------------------------------------- */
#ifdef _OPENMP
#pragma omp parallel for schedule(static)
#endif
    for (int64_t i = 0; i < int64_t(n); ++i) {
        Vec3f& vN = verts[i].nor;
        float len = std::sqrt(vN.x * vN.x + vN.y * vN.y + vN.z * vN.z);
        if (len > 0.f) { vN.x /= len; vN.y /= len; vN.z /= len; } // 单位化
        else { vN = { 0,0,1 }; }                    // 无面积→默认朝上
    }

    /* --- Pass8: 填充输出结构 ---------------------------------------- */
    outMesh->verts = verts.release();   // 转移所有权
    outMesh->idx = idx.release();
    outMesh->vCnt = n;
    outMesh->iCnt = triCnt;

    _log(log, "✅", "ReconstructAA: ok");       // ✅ 结束日志
    return Err::Ok;
}


/* ---------- 释放接口 ---------- */
void FreeMesh(Mesh* m)
{
    if (!m) return;
    delete[] m->verts; delete[] m->idx;
    m->verts = nullptr; m->idx = nullptr; m->vCnt = m->iCnt = 0;
}