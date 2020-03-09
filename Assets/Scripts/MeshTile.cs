using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

/*
Todo: 
- create mesh once, reuse for all tiles
- use material property blocks to manage per-tile data
*/

public struct Vertex {
    public float3 position;
    public float2 uv;
}

public class MeshTile : MonoBehaviour, System.IDisposable {
    private Transform _transform;
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _renderer;

    NativeArray<Vertex> _vertices;

    private int _resolution;
    private int _indexEndTl;
    private int _indexEndTr;
    private int _indexEndBl;
    private int _indexEndBr;

    public Transform Transform {
        get { return _transform; }
    }

    public Mesh Mesh {
        get { return _mesh; }
    }

    public MeshFilter MeshFilter {
        get { return _meshFilter; }
    }

    public MeshRenderer MeshRenderer {
        get { return _renderer; }
    }

    public void Create(int2 dims) {
        _transform = gameObject.GetComponent<Transform>();
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _renderer = gameObject.AddComponent<MeshRenderer>();

        _vertices = new NativeArray<Vertex>(dims.x * dims.y, Allocator.Persistent);
        
        CreateMesh(dims);
    }

    public void Dispose() {
        _vertices.Dispose();
    }

    private void CreateMesh(int2 dims) {
        int numVerts = (dims.x) * (dims.y);
        int numIndices = (dims.x-1) * (dims.y-1) * 2 * 3;
        
        var indices = new NativeArray<uint>(numIndices, Allocator.Temp); // (ushort)?

        for (int y = 0; y < dims.y; y++) {
            for (int x = 0; x < dims.x; x++) {
                _vertices[dims.x * y + x] = new Vertex {
                    position = new float3(
                        x/(float)dims.x * 16f,
                        y/(float)dims.y * 9f,
                        10f),
                    uv = new float2(
                        x / (float)dims.x,
                        y / (float)dims.y),
                };
            }
        }

        CreateIndices(indices, dims);

        _mesh = new Mesh();
        _mesh.hideFlags = HideFlags.DontSave;

        // Question: is there a maximum number for dimension in these configs?

        _mesh.SetVertexBufferParams(
            numVerts,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        );

        var updateFlags = MeshUpdateFlags.Default;
        
        _mesh.SetIndexBufferParams(numIndices, IndexFormat.UInt32);
        _mesh.SetIndexBufferData(indices, 0, 0, numIndices, updateFlags);
        _mesh.SetVertexBufferData(_vertices, 0, 0, numVerts, 0, updateFlags);

        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, numIndices), updateFlags);

        _mesh.RecalculateBounds();
        var bounds = _mesh.bounds;
        var size = bounds.size;
        size.z = 100f;
        bounds.size = size;
        _mesh.bounds = bounds;
        
        _meshFilter.mesh = _mesh;
    }

    public void UpdateVertices(NativeArray<float> depths, int2 dims) {
        var job = new UpdateVertsJob {
            dims = dims,
            depths = depths,
            vertices = _vertices
        };
        var handle = job.Schedule();
        handle.Complete();

        _mesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0);
        _mesh.RecalculateBounds();
    }

    private static void CreateIndices(NativeArray<uint> triangles, int2 dims) {
        int index = 0;
        for (int y = 0; y < dims.y-1; y++) {
            for (int x = 0; x < dims.x-1; x++) {
                triangles[index++] = (uint)((x + 0) + dims.x * (y + 1));
                triangles[index++] = (uint)((x + 1) + dims.x * (y + 0));
                triangles[index++] = (uint)((x + 0) + dims.x * (y + 0));

                triangles[index++] = (uint)((x + 0) + dims.x * (y + 1));
                triangles[index++] = (uint)((x + 1) + dims.x * (y + 1));
                triangles[index++] = (uint)((x + 1) + dims.x * (y + 0));
            }
        }
    }

    [BurstCompile]
    public struct UpdateVertsJob : IJob {
        [ReadOnly] public int2 dims;
        [ReadOnly] public NativeArray<float> depths;
        [WriteOnly] public NativeArray<Vertex> vertices;

        public void Execute() {
            for (int y = 0; y < dims.y; y++) {
                for (int x = 0; x < dims.x; x++) {
                    vertices[dims.x * y + x] = new Vertex
                    {
                        position = new float3(
                            x / (float)dims.x * 16f,
                            y / (float)dims.y * 9f,
                            depths[dims.x * y + x]),
                        uv = new float2(
                            x / (float)dims.x,
                            y / (float)dims.y),
                    };
                }
            }
        }
    }
}
