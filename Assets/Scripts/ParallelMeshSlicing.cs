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

        NativeList<SlicingJobResult> positiveSliced = new NativeList<SlicingJobResult>(_mesh.triangles.Length, Allocator.TempJob);
        NativeList<SlicingJobResult> negativeSliced = new NativeList<SlicingJobResult>(_mesh.triangles.Length, Allocator.TempJob);
        NativeList<SlicingJobResult> positiveStay = new NativeList<SlicingJobResult>(_mesh.triangles.Length, Allocator.TempJob);
        NativeList<SlicingJobResult> negativeStay = new NativeList<SlicingJobResult>(_mesh.triangles.Length, Allocator.TempJob);

        MeshSlicingJob job = new MeshSlicingJob();
        job.plane = _slicePlane;
        job.vertices = new NativeArray<Vector3>(_mesh.vertices, Allocator.TempJob);
        job.normals = new NativeArray<Vector3>(_mesh.normals, Allocator.TempJob);
        job.triangles = new NativeArray<int>(_mesh.triangles, Allocator.TempJob);
        job.uvs = new NativeArray<Vector2>(_mesh.uv, Allocator.TempJob);

        job.positiveStay = positiveStay.AsParallelWriter();
        job.negativeStay = negativeStay.AsParallelWriter();
        job.positiveResults = positiveSliced.AsParallelWriter();
        job.negativeResults = negativeSliced.AsParallelWriter();

        float startTime = Time.realtimeSinceStartup;
        job.Schedule(_mesh.triangles.Length / 3, 1).Complete();
        Debug.Log($"Slice job took {Time.realtimeSinceStartup - startTime}");

        Debug.Log($"Positive tris {positiveStay.Length} Negative tris {negativeStay.Length}");
        Debug.Log($"Positive sliced {positiveSliced.Length} Negative sliced {negativeSliced.Length}");

        job.vertices.Dispose();
        job.normals.Dispose();
        job.triangles.Dispose();
        job.uvs.Dispose();

        if (debugDrawMesh)
        {
            DebugDrawTris(positiveSliced.ToArray(), Color.green);
            DebugDrawTris(positiveStay.ToArray(), Color.green);
            DebugDrawTris(negativeSliced.ToArray(), Color.red);
            DebugDrawTris(negativeStay.ToArray(), Color.red);
        }

        MeshSliceConstructionJob posMesh = new MeshSliceConstructionJob();
        posMesh.stayResults = positiveStay;
        posMesh.sliceResults = positiveSliced;
        posMesh.vertices = new NativeList<Vector3>((positiveSliced.Length + positiveStay.Length) * 3, Allocator.TempJob);
        posMesh.normals = new NativeList<Vector3>((positiveSliced.Length + positiveStay.Length) * 3, Allocator.TempJob);
        posMesh.tris = new NativeArray<int>((positiveSliced.Length + positiveStay.Length) * 3, Allocator.TempJob);
        posMesh.uvs = new NativeList<Vector2>((positiveSliced.Length + positiveStay.Length) * 3, Allocator.TempJob);

        MeshSliceConstructionJob negMesh = new MeshSliceConstructionJob();
        negMesh.stayResults = negativeStay;
        negMesh.sliceResults = negativeSliced;
        negMesh.vertices = new NativeList<Vector3>((negativeSliced.Length + negativeStay.Length) * 3, Allocator.TempJob);
        negMesh.normals = new NativeList<Vector3>((negativeSliced.Length + negativeStay.Length) * 3, Allocator.TempJob);
        negMesh.tris = new NativeArray<int>((negativeSliced.Length + negativeStay.Length) * 3, Allocator.TempJob);
        negMesh.uvs = new NativeList<Vector2>((negativeSliced.Length + negativeStay.Length) * 3, Allocator.TempJob);

        JobHandle pHandle = posMesh.Schedule();
        JobHandle nHandle = negMesh.Schedule();

        pHandle.Complete();
        nHandle.Complete();

        CreateMeshGO(ref posMesh.tris, ref posMesh.vertices, ref posMesh.normals, ref posMesh.uvs, "POSITIVE");
        CreateMeshGO(ref negMesh.tris, ref negMesh.vertices, ref negMesh.normals, ref negMesh.uvs, "NEGATIVE");

        // Disposing of everything
        positiveSliced.Dispose();
        negativeSliced.Dispose();
        positiveStay.Dispose();
        negativeStay.Dispose();

        posMesh.vertices.Dispose();
        posMesh.normals.Dispose();
        posMesh.tris.Dispose();
        posMesh.uvs.Dispose();

        negMesh.vertices.Dispose();
        negMesh.normals.Dispose();
        negMesh.tris.Dispose();
        negMesh.uvs.Dispose();
    }


    private void CreateMeshGO(ref NativeArray<int> tris, ref NativeList<Vector3> vertices, ref NativeList<Vector3> normals, ref NativeList<Vector2> uvs, string name)
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
