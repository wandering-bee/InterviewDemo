#pragma once
#include <cstdint>
#include <cstdlib>

#ifdef _WIN32
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#define DLL_EXPORT extern "C"
#endif

/* ---------- API 枚举 ---------- */
enum class ReconstructMode : uint32_t {
    Subdiv = 0,
    Topology = 1
};

/* ---------- 数据结构（POD） ---------- */
struct Vec3f { float x, y, z; };

/* 与 C# [StructLayout(Pack=1)] 严格一致 */
struct [[gnu::packed]] VertexF {
    Vec3f pos;
    Vec3f nor;
    Vec3f col;
};

struct Mesh {
    VertexF* verts;
    uint32_t* idx;
    uint32_t  vCnt;
    uint32_t  iCnt;
};

enum class Err : uint32_t {
    Ok = 0,
    EmptyInput,
    AllocFail
};

using LogFn = void(*)(const char*);


/* ---------- 导出函数 ---------- */
DLL_EXPORT Err ReconstructAA(
    const Vec3f* pointCloud, uint32_t pointCount,
    ReconstructMode   mode,
    double            topoTolerance,
    Mesh* outMesh,
    LogFn             log /* 可空 */ = nullptr);

DLL_EXPORT void FreeMesh(Mesh* m);
