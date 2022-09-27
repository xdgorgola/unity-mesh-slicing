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


    // NEW
    [WriteOnly]
    public NativeList<int>.ParallelWriter positiveTris;
    [WriteOnly]
    public NativeList<int>.ParallelWriter negativeTris;

    [WriteOnly]
    public NativeList<TrisSliceResult>.ParallelWriter positiveSlices;
    [WriteOnly]
    public NativeList<TrisSliceResult>.ParallelWriter negativeSlices;




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
            bool isPositive = plane.GetSide(vertices[triangles[i]]);
            (isPositive ? positiveTris : negativeTris).AddNoResize(i / 3);
            return;
        }

        Vector3 int1Normal;
        Vector3 int2Normal;
        Vector3 int3Normal;

        Vector2 int1UV;
        Vector2 int2UV;
        Vector2 int3UV;

        TrisSliceResult sr1 = new TrisSliceResult();
        TrisSliceResult sr2 = new TrisSliceResult();
        TrisSliceResult sr3 = new TrisSliceResult();

        if (int1.intersection is null)
        {
            sr1.vaNew = int3.intersection.Value; sr1.vbNew = int2.intersection.Value; sr1.vaOld = triangles[i + 2];
            sr2.vaOld = triangles[i]; sr2.vbOld = triangles[i + 1]; sr2.vaNew = int3.intersection.Value;
            sr3.vaOld = triangles[i + 1]; sr3.vaNew = int2.intersection.Value; sr3.vbNew = int3.intersection.Value;

            sr1.first = -1; sr1.second = -2; sr1.third = 0;
            sr2.first = 0; sr2.second = 1; sr2.third = -1;
            sr3.first = 0; sr3.second = -1; sr3.third = -2;

            int2Normal = Vector3.Lerp(normals[triangles[i + 1]], normals[triangles[i + 2]], int2.interTime);
            int3Normal = Vector3.Lerp(normals[triangles[i + 2]], normals[triangles[i]], int3.interTime);

            sr1.naNew = int3Normal; sr1.nbNew = int2Normal;
            sr2.naNew = int3Normal;
            sr3.naNew = int2Normal; sr3.nbNew = int3Normal;

            int2UV = Vector2.Lerp(uvs[triangles[i + 1]], uvs[triangles[i + 2]], int2.interTime);
            int3UV = Vector2.Lerp(uvs[triangles[i + 2]], uvs[triangles[i]], int3.interTime);

            sr1.uvaNew = int3UV; sr1.uvbNew = int2UV;
            sr2.uvaNew = int3UV;
            sr3.uvaNew = int2UV; sr3.uvbNew = int3UV;

            (!int1.aPositive ? positiveSlices : negativeSlices).AddNoResize(sr1);
            (plane.GetSide(vertices[sr2.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr2);
            (plane.GetSide(vertices[sr3.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr3);

            //
            //sr1.isEdge = true;
            //sr1.edgeA = 0;
            //sr1.edgeB = 1;
            //
            //sr3.isEdge = true;
            //sr3.edgeA = 1;
            //sr3.edgeB = 2;
            return;
        }
        else if (int2.intersection is null)
        {
            sr1.vaNew = int3.intersection.Value; sr1.vaOld = triangles[i]; sr1.vbNew = int1.intersection.Value;
            sr2.vaOld = triangles[i + 1]; sr2.vbOld = triangles[i + 2]; sr2.vaNew = int1.intersection.Value;
            sr3.vaOld = triangles[i + 2]; sr3.vaNew = int3.intersection.Value; sr3.vbNew = int1.intersection.Value;

            sr1.first = -1; sr1.second = 0; sr1.third = -2;
            sr2.first = 0; sr2.second = 1; sr2.third = -1;
            sr3.first = 0; sr3.second = -1; sr3.third = -2;

            int1Normal = Vector3.Lerp(normals[triangles[i]], normals[triangles[i + 1]], int1.interTime);
            int3Normal = Vector3.Lerp(normals[triangles[i + 2]], normals[triangles[i]], int3.interTime);

            sr1.naNew = int3Normal; sr1.nbNew = int1Normal;
            sr2.naNew = int1Normal;
            sr3.naNew = int3Normal; sr3.nbNew = int1Normal;

            int1UV = Vector2.Lerp(uvs[triangles[i]], uvs[triangles[i + 1]], int1.interTime);
            int3UV = Vector2.Lerp(uvs[triangles[i + 2]], uvs[triangles[i]], int3.interTime);

            sr1.uvaNew = int3UV; sr1.uvbNew = int1UV;
            sr2.uvaNew = int1UV;
            sr3.uvaNew = int3UV; sr3.uvbNew = int1UV;

            (!int2.aPositive ? positiveSlices : negativeSlices).AddNoResize(sr1);
            (plane.GetSide(vertices[sr2.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr2);
            (plane.GetSide(vertices[sr3.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr3);

            //
            //sr1.isEdge = true;
            //sr1.edgeA = 0;
            //sr1.edgeB = 2;
            //
            //sr3.isEdge = true;
            //sr3.edgeA = 1;
            //sr3.edgeB = 2;
            //

            return;
        }

        sr1.vaNew = int1.intersection.Value; sr1.vaOld = triangles[i + 1]; sr1.vbNew = int2.intersection.Value;
        sr2.vaOld = triangles[i + 2]; sr2.vbOld = triangles[i]; sr2.vaNew = int2.intersection.Value;
        sr3.vaOld = triangles[i]; sr3.vaNew = int1.intersection.Value; sr3.vbNew = int2.intersection.Value;

        sr1.first = -1; sr1.second = 0; sr1.third = -2;
        sr2.first = 0; sr2.second = 1; sr2.third = -1;
        sr3.first = 0; sr3.second = -1; sr3.third = -2;

        int1Normal = Vector3.Lerp(normals[triangles[i]], normals[triangles[i + 1]], int1.interTime);
        int2Normal = Vector3.Lerp(normals[triangles[i + 1]], normals[triangles[i + 2]], int2.interTime);

        sr1.naNew = int1Normal; sr1.nbNew = int2Normal;
        sr2.naNew = int2Normal;
        sr3.naNew = int1Normal; sr3.nbNew = int2Normal;

        int1UV = Vector2.Lerp(uvs[triangles[i]], uvs[triangles[i + 1]], int1.interTime);
        int2UV = Vector2.Lerp(uvs[triangles[i + 1]], uvs[triangles[i + 2]], int2.interTime);

        sr1.uvaNew = int1UV; sr1.uvbNew = int2UV;
        sr2.uvaNew = int2UV;
        sr3.uvaNew = int1UV; sr3.uvbNew = int2UV;

        (!int3.aPositive ? positiveSlices : negativeSlices).AddNoResize(sr1);
        (plane.GetSide(vertices[sr2.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr2);
        (plane.GetSide(vertices[sr3.vaOld]) ? positiveSlices : negativeSlices).AddNoResize(sr3);

        //
        //sr1.isEdge = true;
        //sr1.edgeA = 0;
        //sr1.edgeB = 2;
        //
        //sr3.isEdge = true;
        //sr3.edgeA = 1;
        //sr3.edgeB = 2;
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


public struct TrisSliceResult
{
    public Vector3 vaNew;
    public Vector3 vbNew;
    public int vaOld;
    public int vbOld;

    public Vector3 naNew;
    public Vector2 uvaNew;

    public Vector3 nbNew;
    public Vector2 uvbNew;

    // 1 -> vbOld
    // 0 -> vaOld
    // -1 -> vaNew
    // -2 -> vbNew
    public int first;
    public int second;
    public int third;
}


public struct MeshSliceConstructionJob : IJob
{
    [ReadOnly]
    public NativeList<int> resultTris;
    [ReadOnly]
    public NativeList<TrisSliceResult> slices;

    [ReadOnly]
    public NativeArray<Vector3> inVertices;
    [ReadOnly]
    public NativeArray<int> inTris;
    [ReadOnly]
    public NativeArray<Vector3> inNormals;
    [ReadOnly]
    public NativeArray<Vector2> inUVs;

    public NativeList<Vector3> outVertices;
    [WriteOnly]
    public NativeList<int> outTris; // COULD BE AN ARRAY.
    [WriteOnly]
    public NativeList<Vector3> outNormals;
    [WriteOnly]
    public NativeList<Vector2> outUVs;


    public void Execute()
    {
        Dictionary<int, int> indexMap = new Dictionary<int, int>((resultTris.Length + slices.Length) * 3);
        Dictionary<int, int> sliceMap = new Dictionary<int, int>((resultTris.Length + slices.Length) * 3);

        for (int i = 0; i < resultTris.Length; ++i)
            CheckNormalTris(i, indexMap);

        for (int i = 0; i < slices.Length; ++i)
            CheckSliceTris(i, indexMap, sliceMap);
    }


    private void CheckNormalTris(int trisStart, Dictionary<int, int> indexMap)
    {
        int trisIndex = resultTris[trisStart] * 3;

        for (int i = 0; i < 3; ++i)
        {
            int curr = inTris[trisIndex + i];

            if (indexMap.ContainsKey(curr))
            {
                outTris.AddNoResize(indexMap[curr]);
                continue;
            }

            indexMap.Add(curr, indexMap.Count);
            outTris.AddNoResize(indexMap[curr]);
            outVertices.AddNoResize(inVertices[curr]);
            
            outNormals.AddNoResize(inNormals[curr]);
            outUVs.AddNoResize(inUVs[curr]);
        }
    }


    private void CheckSliceTris(int sliceIndex, Dictionary<int, int> trisMap, Dictionary<int, int> sliceMap)
    {
        TrisSliceResult res = slices[sliceIndex];
        ProcessSliceVertex(res.first, ref res, trisMap, sliceMap);
        ProcessSliceVertex(res.second, ref res, trisMap, sliceMap);
        ProcessSliceVertex(res.third, ref res, trisMap, sliceMap);
    }


    // TODO: New vertices realmap
    private void ProcessSliceVertex(int vertexID, ref TrisSliceResult slice, Dictionary<int, int> normalMap, Dictionary<int, int> sliceMap)
    {
        switch (vertexID)
        {
            case 1:
                if (normalMap.ContainsKey(slice.vbOld))
                {
                    outTris.AddNoResize(normalMap[slice.vbOld]);
                    break;
                }

                normalMap.Add(slice.vbOld, outVertices.Length);
                outTris.AddNoResize(normalMap[slice.vbOld]);
                outVertices.AddNoResize(inVertices[slice.vbOld]);
                outNormals.AddNoResize(inNormals[slice.vbOld]);
                outUVs.AddNoResize(inUVs[slice.vbOld]);
                break;
            case 0:
                if (normalMap.ContainsKey(slice.vaOld))
                {
                    outTris.AddNoResize(normalMap[slice.vaOld]);
                    break;
                }

                normalMap.Add(slice.vaOld, outVertices.Length);
                outTris.AddNoResize(normalMap[slice.vaOld]);
                outVertices.AddNoResize(inVertices[slice.vaOld]);
                outNormals.AddNoResize(inNormals[slice.vaOld]);
                outUVs.AddNoResize(inUVs[slice.vaOld]);
                break;
            case -1:
                //sliceMap.Add(outVertices.Length, outVertices.Length);
                outTris.AddNoResize(outVertices.Length);
                outVertices.AddNoResize(slice.vaNew);
                outNormals.AddNoResize(slice.naNew);
                outUVs.AddNoResize(slice.uvaNew);
                break;
            case -2:
                //sliceMap.Add(outVertices.Length, outVertices.Length);
                outTris.AddNoResize(outVertices.Length);
                outVertices.AddNoResize(slice.vbNew);
                outNormals.AddNoResize(slice.nbNew);
                outUVs.AddNoResize(slice.uvbNew);
                break;
            default:
                break;
        }
    }
}