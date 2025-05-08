#pragma once
#include <cstdint>
#include <cstdlib>

#ifdef _WIN32
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#define DLL_EXPORT extern "C"
#endif

/* ---------- API ö�� ---------- */
enum class ReconstructMode : uint32_t {
    Subdiv = 0,
    Topology = 1
};

/* ---------- ���ݽṹ��POD�� ---------- */
struct Vec3f { float x, y, z; };

/* �� C# [StructLayout(Pack=1)] �ϸ�һ�� */
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


/* ---------- �������� ---------- */
DLL_EXPORT Err ReconstructAA(
    const Vec3f* pointCloud, uint32_t pointCount,
    ReconstructMode   mode,
    double            topoTolerance,
    Mesh* outMesh,
    LogFn             log /* �ɿ� */ = nullptr);

DLL_EXPORT void FreeMesh(Mesh* m);
