using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshSlicer : MonoBehaviour
{
    private MeshFilter _filter;
    private Mesh _mesh;

    [SerializeField]
    private Vector3 _planeNormal = Vector3.right;
    [SerializeField]
    private Vector3 _planeObjPos;
    private Plane _slicePlane;

    public Material testMaterial;
    public Vector3 testOffset = Vector3.zero;
    public float testSeparation = 0f;
    public bool testDrawMesh = false;


    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            _mesh = _filter.mesh;
            _planeNormal = _planeNormal.normalized;
            _slicePlane = new Plane(_planeNormal, _planeObjPos);
            Slice();
        }
    }


    public void Slice()
    {
        Dictionary<Vector3, float> vDist = new Dictionary<Vector3, float>(_mesh.vertices.Length);

        int vCount = _mesh.vertexCount;
        List<Triangle> triangles = new List<Triangle>(Mathf.CeilToInt(_mesh.triangles.Length / 3));
        for (int i = 0; i < _mesh.triangles.Length; i += 3)
        {
            Triangle nt = new Triangle();
            nt.tri = new int[3] { _mesh.triangles[i], _mesh.triangles[i + 1], _mesh.triangles[i + 2] }; //_mesh.triangles[i..i+3]
            nt.normals = new Vector3[3] { _mesh.normals[nt.tri[0]], _mesh.normals[nt.tri[1]], _mesh.normals[nt.tri[2]] };
            nt.vertices = new Vector3[3] { _mesh.vertices[nt.tri[0]], _mesh.vertices[nt.tri[1]], _mesh.vertices[nt.tri[2]] };

            (var res, var ind) = IntersectTriAndPlane(ref nt, ref _slicePlane, vCount, vDist);   
            vCount = ind;

            triangles.Add(res.a);

            if (res.b.HasValue)
                triangles.Add(res.b.Value);

            if (res.c.HasValue)
                triangles.Add(res.c.Value);
        }

        if (testDrawMesh)
        {
            foreach (Triangle t in triangles)
            {
                Vector3 offset = transform.position + testOffset * testSeparation;
                if (!t.positive)
                    offset *= -1;

                Debug.DrawLine(transform.rotation * (offset + t.vertices[0]), transform.rotation * (offset + t.vertices[1]), Color.green, 10f);
                Debug.DrawLine(transform.rotation * (offset + t.vertices[0]), transform.rotation * (offset + t.vertices[2]), Color.green, 10f);
                Debug.DrawLine(transform.rotation * (offset + t.vertices[1]), transform.rotation * (offset + t.vertices[2]), Color.green, 10f);
            }
        }

        //Triangle[] positiveGroup = triangles.Where(t => t.positive).ToArray();
        //Triangle[] negativeGroup = triangles.Where(t => !t.positive).ToArray();
        //
        ////Debug.Log($"Positive Group count {positiveGroup.Length}");
        ////Debug.Log($"Negative Group count {negativeGroup.Length}");
        //
        //GameObject pg = new GameObject("PG");
        //GameObject ng = new GameObject("NG");
        //
        //MeshFilter pgmf = pg.AddComponent<MeshFilter>();
        //MeshRenderer pgre = pg.AddComponent<MeshRenderer>();
        //
        //MeshFilter ngmf = ng.AddComponent<MeshFilter>();
        //MeshRenderer ngre = ng.AddComponent<MeshRenderer>();
        //
        //pgmf.mesh = CreateMesh(positiveGroup, "PG");
        //ngmf.mesh = CreateMesh(negativeGroup, "NG");
        //pgre.material = testMaterial;
        //ngre.material = testMaterial;
    }


    private void IntersectSegmentAndPlane(ref Vector3 pA, ref Vector3 pB, ref Plane plane, Dictionary<Vector3, float> vDist, out PlaneSegInter inter)
    {
        if (!vDist.ContainsKey(pA))
            vDist.Add(pA, plane.GetDistanceToPoint(pA));

        if (!vDist.ContainsKey(pB))
            vDist.Add(pB, plane.GetDistanceToPoint(pB));

        float dA = vDist[pA];
        float dB = vDist[pB];

        bool aPositive = dA >= 0;
        bool bPositive = dB >= 0;

        if (aPositive == bPositive)
        {
            //Debug.Log($"{pA} and {pB} are in the same side of the plane.");
            inter.aPositive = aPositive;
            inter.bPositive = bPositive;
            inter.interTime = 0f;
            inter.intersection = null;
            return;
        }

        float t = dA / (dA - dB);
        //Debug.Log($"{pA} and {pB} intersect the plane at {pA + t * (pB - pA)}.");

        inter.aPositive = aPositive;
        inter.bPositive = bPositive;
        inter.interTime = t;
        inter.intersection = pA + t * (pB - pA);
    }


    private (PlaneTriInter, int) IntersectTriAndPlane(ref Triangle tri, ref Plane plane, int ind, Dictionary<Vector3, float> vDist)
    {
        (Vector3 a, Vector3 b) seg1 = (tri.vertices[0], tri.vertices[1]);
        (Vector3 a, Vector3 b) seg2 = (tri.vertices[1], tri.vertices[2]);
        (Vector3 a, Vector3 b) seg3 = (tri.vertices[2], tri.vertices[0]);

        IntersectSegmentAndPlane(ref seg1.a, ref seg1.b, ref plane, vDist, out PlaneSegInter int1);
        IntersectSegmentAndPlane(ref seg2.a, ref seg2.b, ref plane, vDist, out PlaneSegInter int2);
        IntersectSegmentAndPlane(ref seg3.a, ref seg3.b, ref plane, vDist, out PlaneSegInter int3);

        PlaneTriInter res = new PlaneTriInter();
        if (int1.intersection is null && int2.intersection is null && int3.intersection is null)
        {
            res.a = tri;
            res.a.positive = plane.GetSide(tri.vertices[0]);
            res.b = null;
            res.c = null;

            //Debug.Log("No intersection");
            return (res, ind);
        }

        Triangle sing = new Triangle();
        Triangle bVal = new Triangle();
        Triangle cVal = new Triangle();

        Vector3 int1Normal;
        Vector3 int2Normal;
        Vector3 int3Normal;

        sing.tri = new int[] { ind, ind + 1, ind + 2 };
        
        if (int1.intersection is null)
        {
            sing.vertices = new Vector3[3] { int3.intersection.Value, int2.intersection.Value, tri.vertices[2] };
            bVal.vertices = new Vector3[3] { tri.vertices[0], tri.vertices[1], int3.intersection.Value};
            cVal.vertices = new Vector3[3] { tri.vertices[1], int2.intersection.Value, int3.intersection.Value };

            sing.positive = !int1.aPositive;
            bVal.positive = vDist[bVal.vertices[0]] >= 0;
            cVal.positive = vDist[cVal.vertices[0]] >= 0;

            int2Normal = Vector3.Lerp(tri.normals[1], tri.normals[2], int2.interTime);
            int3Normal = Vector3.Lerp(tri.normals[2], tri.normals[0], int3.interTime);

            sing.normals = new Vector3[3] { int3Normal, int2Normal, tri.normals[2] };
            bVal.normals = new Vector3[3] { tri.normals[0], tri.normals[1], int3Normal };
            cVal.normals = new Vector3[3] { tri.normals[1], int2Normal, int3Normal };

            res.a = sing;
            res.b = bVal;
            res.c = cVal;

            return (res, ind + 9);
        }
        else if (int2.intersection is null)
        {
            sing.vertices = new Vector3[3] { int3.intersection.Value, tri.vertices[0], int1.intersection.Value };
            bVal.vertices = new Vector3[3] { tri.vertices[1], tri.vertices[2], int1.intersection.Value };
            cVal.vertices = new Vector3[3] { tri.vertices[2], int3.intersection.Value, int1.intersection.Value };

            sing.positive = !int2.aPositive;
            bVal.positive = vDist[bVal.vertices[0]] >= 0;
            cVal.positive = vDist[cVal.vertices[0]] >= 0;

            int1Normal = Vector3.Lerp(tri.normals[0], tri.normals[1], int1.interTime);
            int3Normal = Vector3.Lerp(tri.normals[2], tri.normals[0], int3.interTime);

            sing.normals = new Vector3[3] { int3Normal, tri.normals[0], int1Normal };
            bVal.normals = new Vector3[3] { tri.normals[1], tri.normals[2], int1Normal };
            cVal.normals = new Vector3[3] { tri.normals[2], int3Normal, int1Normal };

            res.a = sing;
            res.b = bVal;
            res.c = cVal;

            return (res, ind + 9);
        }

        sing.vertices = new Vector3[3] { int1.intersection.Value, tri.vertices[1], int2.intersection.Value };
        bVal.vertices = new Vector3[3] { tri.vertices[2], tri.vertices[0], int2.intersection.Value };
        cVal.vertices = new Vector3[3] { tri.vertices[0], int1.intersection.Value, int2.intersection.Value };

        sing.positive = !int3.aPositive;
        bVal.positive = vDist[bVal.vertices[0]] >= 0;
        cVal.positive = vDist[cVal.vertices[0]] >= 0;

        int1Normal = Vector3.Lerp(tri.normals[0], tri.normals[1], int1.interTime);
        int2Normal = Vector3.Lerp(tri.normals[1], tri.normals[2], int2.interTime);

        sing.normals = new Vector3[3] { int1Normal, tri.normals[1], int2Normal };
        bVal.normals = new Vector3[3] { tri.normals[2], tri.normals[0], int2Normal };
        cVal.normals = new Vector3[3] { tri.normals[0], int1Normal, int2Normal };

        res.a = sing;
        res.b = bVal;
        res.c = cVal;

        return (res, ind + 9);
    }


    private Mesh CreateMesh(Triangle[] triangles, string name = "DEFAULT")
    {
        Mesh nm = new Mesh();
        nm.name = name;

        Dictionary<Vector3, int> verIndices = new Dictionary<Vector3, int>();
        Dictionary<int, Vector3> verNormals = new Dictionary<int, Vector3>();
        
        foreach (Triangle t in triangles)
        {
            for (int i = 0; i < t.vertices.Count(); ++i)
            {
                if (verIndices.ContainsKey(t.vertices[i]))
                    continue;

                verIndices.Add(t.vertices[i], verIndices.Count);
                verNormals.Add(verNormals.Count, t.normals[i]);
            }
        }

        Vector3[] vertices = new Vector3[verIndices.Count];
        foreach (Vector3 v in verIndices.Keys)
            vertices[verIndices[v]] = v;

        List<int> indices = new List<int>();
        foreach (Triangle t in triangles)
        {
            indices.Add(verIndices[t.vertices[0]]);
            indices.Add(verIndices[t.vertices[1]]);
            indices.Add(verIndices[t.vertices[2]]);
        }

        Vector3[] normals = new Vector3[vertices.Count()];
        for (int i = 0; i < vertices.Count(); ++i)
            normals[i] = verNormals[i];

        nm.vertices = vertices;
        nm.triangles = indices.ToArray();
        nm.normals = normals;

        return nm;
    }
}
