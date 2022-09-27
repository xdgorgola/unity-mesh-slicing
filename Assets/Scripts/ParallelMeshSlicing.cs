using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class ParallelMeshSlicing : MonoBehaviour
{
    private MeshFilter _filter;
    private Mesh _mesh;

    [SerializeField]
    private Vector3 _planeNormal = Vector3.right;
    [SerializeField]
    private Vector3 _planeObjPos;
    private Plane _slicePlane;

    public Material debugMaterial;
    public Vector3 debugOffset = Vector3.zero;
    public bool debugDrawMesh = false;


    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Slice();
        }
    }


    private void Slice()
    {
        _mesh = _filter.mesh;
        _planeNormal = _planeNormal.normalized;
        _slicePlane = new Plane(_planeNormal, _planeObjPos);

        NativeList<int> positiveTris = new NativeList<int>(_mesh.triangles.Length / 3, Allocator.TempJob);
        NativeList<int> negativeTris = new NativeList<int>(_mesh.triangles.Length / 3, Allocator.TempJob);
        NativeList<TrisSliceResult> positiveSlices = new NativeList<TrisSliceResult>(_mesh.triangles.Length, Allocator.TempJob);
        NativeList<TrisSliceResult> negativeSlices = new NativeList<TrisSliceResult>(_mesh.triangles.Length, Allocator.TempJob);

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(_mesh.vertices, Allocator.TempJob);
        NativeArray<Vector3> normals = new NativeArray<Vector3>(_mesh.normals, Allocator.TempJob);
        NativeArray<int> tris = new NativeArray<int>(_mesh.triangles, Allocator.TempJob);
        NativeArray<Vector2> uvs = new NativeArray<Vector2>(_mesh.uv, Allocator.TempJob);

        MeshSlicingJob job = new MeshSlicingJob();
        job.plane = _slicePlane;
        job.vertices = vertices;
        job.normals = normals;
        job.triangles = tris;
        job.uvs = uvs;

        job.positiveTris = positiveTris.AsParallelWriter();
        job.negativeTris = negativeTris.AsParallelWriter();
        job.positiveSlices = positiveSlices.AsParallelWriter();
        job.negativeSlices = negativeSlices.AsParallelWriter();


        float startTime = Time.realtimeSinceStartup;
        job.Schedule(_mesh.triangles.Length / 3, 1).Complete();
        Debug.Log($"Slice job took {Time.realtimeSinceStartup - startTime}");
        
        Debug.Log($"Positive tris {positiveTris.Length} Negative tris {negativeTris.Length}");
        Debug.Log($"Positive sliced {positiveSlices.Length} Negative sliced {negativeSlices.Length}");

        MeshSliceConstructionJob posMesh = new MeshSliceConstructionJob();
        posMesh.resultTris = positiveTris;
        posMesh.slices = positiveSlices;

        posMesh.inVertices = vertices;
        posMesh.inNormals = normals;
        posMesh.inTris = tris;
        posMesh.inUVs = uvs;

        // Checar capacidades
        posMesh.outVertices = new NativeList<Vector3>((positiveSlices.Length + positiveTris.Length) * 3, Allocator.TempJob);
        posMesh.outNormals = new NativeList<Vector3>((positiveSlices.Length + positiveTris.Length) * 3, Allocator.TempJob);
        posMesh.outTris = new NativeList<int>((positiveSlices.Length + positiveTris.Length) * 3, Allocator.TempJob);
        posMesh.outUVs = new NativeList<Vector2>((positiveSlices.Length + positiveTris.Length) * 3, Allocator.TempJob);
        
        MeshSliceConstructionJob negMesh = new MeshSliceConstructionJob();
        negMesh.resultTris = negativeTris;
        negMesh.slices = negativeSlices;

        negMesh.inVertices = vertices;
        negMesh.inNormals = normals;
        negMesh.inTris = tris;
        negMesh.inUVs = uvs;

        // Checar capacidades
        negMesh.outVertices = new NativeList<Vector3>((negativeSlices.Length + negativeTris.Length) * 3, Allocator.TempJob);
        negMesh.outNormals = new NativeList<Vector3>((negativeSlices.Length + negativeTris.Length) * 3, Allocator.TempJob);
        negMesh.outTris = new NativeList<int>((negativeSlices.Length + negativeTris.Length) * 3, Allocator.TempJob);
        negMesh.outUVs = new NativeList<Vector2>((negativeSlices.Length + negativeTris.Length) * 3, Allocator.TempJob);

        JobHandle pHandle = posMesh.Schedule();
        JobHandle nHandle = negMesh.Schedule();
        
        pHandle.Complete();
        nHandle.Complete();
        
        CreateMeshGO(ref posMesh.outTris, ref posMesh.outVertices, ref posMesh.outNormals, ref posMesh.outUVs, "POSITIVE");
        CreateMeshGO(ref negMesh.outTris, ref negMesh.outVertices, ref negMesh.outNormals, ref negMesh.outUVs, "NEGATIVE");

        // Disposing of everything
        vertices.Dispose();
        normals.Dispose();
        tris.Dispose();
        uvs.Dispose();

        positiveTris.Dispose();
        negativeTris.Dispose();
        positiveSlices.Dispose();
        negativeSlices.Dispose();

        posMesh.outVertices.Dispose();
        posMesh.outNormals.Dispose();
        posMesh.outTris.Dispose();
        posMesh.outUVs.Dispose();
        negMesh.outVertices.Dispose();
        negMesh.outNormals.Dispose();
        negMesh.outTris.Dispose();
        negMesh.outUVs.Dispose();
    }


    private void CreateMeshGO(ref NativeList<int> tris, ref NativeList<Vector3> vertices, ref NativeList<Vector3> normals, ref NativeList<Vector2> uvs, string name)
    {
        Mesh mesh = new Mesh();

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.name = name;

        GameObject meshGO = new GameObject(name);

        MeshFilter meshFilter = meshGO.AddComponent<MeshFilter>();
        MeshRenderer meshRender = meshGO.AddComponent<MeshRenderer>();
        meshRender.material = debugMaterial;
        meshFilter.mesh = mesh;
    }


    private void DebugDrawTris(IEnumerable<SlicingJobResult> tris, Color col)
    {
        foreach (SlicingJobResult t in tris)
        {
            Vector3 offset = transform.position + debugOffset * 0.1f;
            if (!t.positive)
                offset *= -1;

            Debug.DrawLine(transform.rotation * (offset + t.a), transform.rotation * (offset + t.b), col, 10f);
            Debug.DrawLine(transform.rotation * (offset + t.a), transform.rotation * (offset + t.c), col, 10f);
            Debug.DrawLine(transform.rotation * (offset + t.b), transform.rotation * (offset + t.c), col, 10f);
        }
    }
}
