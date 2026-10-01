using UnityEngine;

namespace XpressMediaVR
{
    /// Builds a curved rectangular mesh (a cylindrical segment) at runtime —
    /// the Feed's video wall wraps gently around the viewer instead of
    /// sitting on a flat plane. Procedural on purpose: nothing here can
    /// silently fail to import like a hand-authored .fbx could.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CurvedMeshGenerator : MonoBehaviour
    {
        [Tooltip("Total width of the curved wall, in meters.")]
        public float width = 3.2f;
        [Tooltip("Height of the wall, in meters.")]
        public float height = 1.8f;
        [Tooltip("Radius of the curve — smaller wraps tighter around the viewer.")]
        public float radius = 2.2f;
        [Tooltip("Quads across the width — higher is smoother, costs more.")]
        public int segments = 24;

        private void Awake() => Generate();

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            var mesh = new Mesh { name = "CurvedVideoWall" };
            var totalAngle = width / radius;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = -totalAngle / 2f + totalAngle * t;
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius; // arc centered "radius" meters in front of local origin

                vertices[i * 2] = new Vector3(x, -height / 2f, z);
                vertices[i * 2 + 1] = new Vector3(x, height / 2f, z);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                triangles[ti++] = c; triangles[ti++] = b; triangles[ti++] = d;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = mesh;
        }
    }
}
