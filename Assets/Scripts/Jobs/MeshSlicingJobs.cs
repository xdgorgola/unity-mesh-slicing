using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using System.Collections.Generic;

[BurstCompile]
public struct MeshSlicingJob : IJobParallelFor
{
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> vertices;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> normals;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<int> triangles;
    [ReadOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector2> uvs;

    [ReadOnly]
    public Plane plane;

    [WriteOnly]
    public NativeList<SlicingJobResult>.ParallelWriter positiveResults;
    [WriteOnly]
    public NativeList<SlicingJobResult>.ParallelWriter negativeResults;
    [WriteOnly]
    public NativeList<SlicingJobResult>.ParallelWriter positiveStay;
    [WriteOnly]
    public NativeList<SlicingJobResult>.ParallelWriter negativeStay;


    public void Execute(int index)
    {
        IntersectTriAndPlane(ref plane, index * 3);
    }


    private void IntersectSegmentAndPlane(ref Vector3 pA, ref Vector3 pB, ref Plane plane, out PlaneSegInter r)
    {
        float dA = plane.GetDistanceToPoint(pA);
        float dB = plane.GetDistanceToPoint(pB);
            
        bool aPositive = dA >= 0;
        bool bPositive = dB >= 0;

        r.aPositive = aPositive;
        r.bPositive = bPositive;

        if (aPositive == bPositive)
        {
            r.interTime = 0f;
            r.intersection = null;
            return;
        }

        float t = dA / (dA - dB);
        r.interTime = t;
        r.intersection = pA + t * (pB - pA);
    }


    private void IntersectTriAndPlane(ref Plane plane, int i)
    {
        (Vector3 a, Vector3 b) seg1 = (vertices[triangles[i]], vertices[triangles[i + 1]]);
        (Vector3 a, Vector3 b) seg2 = (vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
        (Vector3 a, Vector3 b) seg3 = (vertices[triangles[i + 2]], vertices[triangles[i]]);

        IntersectSegmentAndPlane(ref seg1.a, ref seg1.b, ref plane, out PlaneSegInter int1);
        IntersectSegmentAndPlane(ref seg2.a, ref seg2.b, ref plane, out PlaneSegInter int2);
        IntersectSegmentAndPlane(ref seg3.a, ref seg3.b, ref plane, out PlaneSegInter int3);

        if ((int1.intersection is null && int2.intersection is null) ||
            (int1.intersection is null && int3.intersection is null) ||
            (int2.intersection is null && int3.intersection is null))
        {
            SlicingJobResult sr = new SlicingJobResult();
            sr.a = vertices[triangles[i]];
            sr.b = vertices[triangles[i + 1]];
            sr.c = vertices[triangles[i + 2]];

            sr.na = normals[triangles[i]];
            sr.nb = normals[triangles[i + 1]];
            sr.nc = normals[triangles[i + 2]];

            sr.uva = uvs[triangles[i]];
            sr.uvb = uvs[triangles[i + 1]];
            sr.uvc = uvs[triangles[i + 2]];

            sr.positive = plane.GetSide(vertices[triangles[i]]);

            (sr.positive ? positiveStay : negativeStay).AddNoResize(sr);
            return;
        }

        Vector3 int1Normal;
        Vector3 int2Normal;
        Vector3 int3Normal;

        Vector2 int1UV;
        Vector2 int2UV;
        Vector2 int3UV;

        SlicingJobResult sr1 = new SlicingJobResult();
        SlicingJobResult sr2 = new SlicingJobResult();
        SlicingJobResult sr3 = new SlicingJobResult();
        if (int1.intersection is null)
        {
            sr1.a = int3.intersection.Value; sr1.b = int2.intersection.Value; sr1.c = vertices[triangles[i + 2]];
            sr2.a = vertices[triangles[i]]; sr2.b = vertices[triangles[i + 1]]; sr2.c = int3.intersection.Value;
            sr3.a = vertices[triangles[i + 1]]; sr3.b = int2.intersection.Value; sr3.c = int3.intersection.Value;

            sr1.positive = !int1.aPositive;
            sr2.positive = plane.GetSide(sr2.a);
            sr3.positive = plane.GetSide(sr3.a);

            sr1.isEdge = true;
            sr1.edgeA = 0;
            sr1.edgeB = 1;

            sr3.isEdge = true;
            sr3.edgeA = 1;
            sr3.edgeB = 2;

            int2Normal = Vector3.Lerp(normals[triangles[i + 1]], normals[triangles[i + 2]], int2.interTime);
            int3Normal = Vector3.Lerp(normals[triangles[i + 2]], normals[triangles[i]], int3.interTime);

            sr1.na = int3Normal; sr1.nb = int2Normal; sr1.nc = normals[triangles[i + 2]];
            sr2.na = normals[triangles[i]]; sr2.nb = normals[triangles[i + 1]]; sr2.nc = int3Normal;
            sr3.na = normals[triangles[i + 1]]; sr3.nb = int2Normal; sr3.nc = int3Normal;

            int2UV = Vector2.Lerp(uvs[triangles[i + 1]], uvs[triangles[i + 2]], int2.interTime);
            int3UV = Vector2.Lerp(uvs[triangles[i + 2]], uvs[triangles[i]], int3.interTime);

            sr1.uva = int3UV; sr1.uvb = int2UV; sr1.uvc = uvs[triangles[i + 2]];
            sr2.uva = uvs[triangles[i]]; sr2.uvb = uvs[triangles[i + 1]]; sr2.uvc = int3UV;
            sr3.uva = uvs[triangles[i + 1]]; sr3.uvb = int3UV; sr3.uvc = int3UV;

            (sr1.positive ? positiveResults : negativeResults).AddNoResize(sr1);
            (sr2.positive ? positiveResults : negativeResults).AddNoResize(sr2);
            (sr3.positive ? positiveResults : negativeResults).AddNoResize(sr3);
            return;
        }
        else if (int2.intersection is null)
        {
            sr1.a = int3.intersection.Value; sr1.b = vertices[triangles[i]]; sr1.c = int1.intersection.Value;
            sr2.a = vertices[triangles[i + 1]]; sr2.b = vertices[triangles[i + 2]]; sr2.c = int1.intersection.Value;
            sr3.a = vertices[triangles[i + 2]]; sr3.b = int3.intersection.Value; sr3.c = int1.intersection.Value;

            sr1.positive = !int2.aPositive;
            sr2.positive = plane.GetSide(sr2.a);
            sr3.positive = plane.GetSide(sr3.a);

            sr1.isEdge = true;
            sr1.edgeA = 0;
            sr1.edgeB = 2;

            sr3.isEdge = true;
            sr3.edgeA = 1;
            sr3.edgeB = 2;

            int1Normal = Vector3.Lerp(normals[triangles[i]], normals[triangles[i + 1]], int1.interTime);
            int3Normal = Vector3.Lerp(normals[triangles[i + 2]], normals[triangles[i]], int3.interTime);

            sr1.na = int3Normal; sr1.nb = normals[triangles[i]]; sr1.nc = int1Normal;
            sr2.na = normals[triangles[i + 1]]; sr2.nb = normals[triangles[i + 2]]; sr2.nc = int1Normal;
            sr3.na = normals[triangles[i + 2]]; sr3.nb = int3Normal; sr3.nc = int1Normal;

            int1UV = Vector2.Lerp(uvs[triangles[i]], uvs[triangles[i + 1]], int1.interTime);
            int3UV = Vector2.Lerp(uvs[triangles[i + 2]], uvs[triangles[i]], int3.interTime);

            sr1.uva = int3UV; sr1.uvb = uvs[triangles[i]]; sr1.uvc = int1UV;
            sr2.uva = uvs[triangles[i + 1]]; sr2.uvb = uvs[triangles[i + 2]]; sr2.uvc = int1UV;
            sr3.uva = uvs[triangles[i + 2]]; sr3.uvb = int3UV; sr3.uvc = int1UV;

            (sr1.positive ? positiveResults : negativeResults).AddNoResize(sr1);
            (sr2.positive ? positiveResults : negativeResults).AddNoResize(sr2);
            (sr3.positive ? positiveResults : negativeResults).AddNoResize(sr3);
            return;
        }

        sr1.a = int1.intersection.Value; sr1.b = vertices[triangles[i + 1]]; sr1.c = int2.intersection.Value;
        sr2.a = vertices[triangles[i + 2]]; sr2.b = vertices[triangles[i]]; sr2.c = int2.intersection.Value;
        sr3.a = vertices[triangles[i]]; sr3.b = int1.intersection.Value; sr3.c = int2.intersection.Value;

        sr1.positive = !int3.aPositive;
        sr2.positive = plane.GetSide(sr2.a);
        sr3.positive = plane.GetSide(sr3.a);

        sr1.isEdge = true;
        sr1.edgeA = 0;
        sr1.edgeB = 2;

        sr3.isEdge = true;
        sr3.edgeA = 1;
        sr3.edgeB = 2;

        int1Normal = Vector3.Lerp(normals[triangles[i]], normals[triangles[i + 1]], int1.interTime);
        int2Normal = Vector3.Lerp(normals[triangles[i + 1]], normals[triangles[i + 2]], int2.interTime);

        sr1.na = int1Normal; sr1.nb = normals[triangles[i + 1]]; sr1.nc = int2Normal;
        sr2.na = normals[triangles[i + 2]]; sr2.nb = normals[triangles[i]]; sr2.nc = int2Normal;
        sr3.na = normals[triangles[i]]; sr3.nb = int1Normal; sr3.nc = int2Normal;

        int1UV = Vector2.Lerp(uvs[triangles[i]], uvs[triangles[i + 1]], int1.interTime);
        int2UV = Vector2.Lerp(uvs[triangles[i + 1]], uvs[triangles[i + 2]], int2.interTime);

        sr1.uva = int1UV; sr1.uvb = uvs[triangles[i + 1]]; sr1.uvc = int2UV;
        sr2.uva = uvs[triangles[i + 2]]; sr2.uvb = uvs[triangles[i]]; sr2.uvc = int2UV;
        sr3.uva = uvs[triangles[i]]; sr3.uvb = int1UV; sr3.uvc = int2UV;

        (sr1.positive ? positiveResults : negativeResults).AddNoResize(sr1);
        (sr2.positive ? positiveResults : negativeResults).AddNoResize(sr2);
        (sr3.positive ? positiveResults : negativeResults).AddNoResize(sr3);
    }
}


public struct SlicingJobResult
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public Vector3 na;
    public Vector3 nb;
    public Vector3 nc;

    public Vector2 uva;
    public Vector2 uvb;
    public Vector2 uvc;
        
    public bool positive;

    public bool isEdge;
    public int edgeA;
    public int edgeB;
}


public struct MeshSliceConstructionJob : IJob
{
    [ReadOnly]
    public NativeArray<SlicingJobResult> stayResults;
    [ReadOnly]
    public NativeArray<SlicingJobResult> sliceResults;

    [WriteOnly]
    public NativeList<Vector3> vertices;
    [WriteOnly]
    public NativeArray<int> tris;
    [WriteOnly]
    public NativeList<Vector3> normals;
    [WriteOnly]
    public NativeList<Vector2> uvs;


    public void Execute()
    {
       Dictionary<Vector3, int> verIndices = new Dictionary<Vector3, int>((stayResults.Length + sliceResults.Length) * 3);

        for (int i = 0; i < stayResults.Length; ++i)
        {
            SlicingJobResult tri = stayResults[i];
            int ind1 = AddVertex(ref tri.a, ref tri.na, ref tri.uva, verIndices);
            int ind2 = AddVertex(ref tri.b, ref tri.nb, ref tri.uvb, verIndices);
            int ind3 = AddVertex(ref tri.c, ref tri.nc, ref tri.uvc, verIndices);

            int init = i * 3;
            
            tris[init] = ind1;
            tris[init + 1] = ind2;
            tris[init + 2] = ind3;
        }

        for (int i = 0; i < sliceResults.Length; ++i)
        {
            SlicingJobResult tri = sliceResults[i];
            int ind1 = AddVertex(ref tri.a, ref tri.na, ref tri.uva, verIndices);
            int ind2 = AddVertex(ref tri.b, ref tri.nb, ref tri.uvb, verIndices);
            int ind3 = AddVertex(ref tri.c, ref tri.nc, ref tri.uvc, verIndices);

            int init = (i + stayResults.Length) * 3;

            tris[init] = ind1;
            tris[init + 1] = ind2;
            tris[init + 2] = ind3;
        }
    }


    private int AddVertex(ref Vector3 vertex, ref Vector3 normal, ref Vector2 uv, Dictionary<Vector3, int> verIndices)
    {
        if (verIndices.ContainsKey(vertex))
            return verIndices[vertex];

        verIndices.Add(vertex, verIndices.Count);

        vertices.AddNoResize(vertex);
        normals.AddNoResize(normal);
        uvs.AddNoResize(uv);

        return verIndices.Count - 1;
    }
}