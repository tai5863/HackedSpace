using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[ExecuteInEditMode]
[RequireComponent(typeof(VisualEffect))]
public class GenerateDynamicAttributeMapsFromMesh : MonoBehaviour
{

    struct ThreadSize
    {
        public int x;
        public int y;
        public int z;

        public ThreadSize(uint x, uint y, uint z)
        {
            this.x = (int)x;
            this.y = (int)y;
            this.z = (int)z;
        }
    }

    [SerializeField]
    MeshFilter meshPrefab;
    [SerializeField] int maxPoints = 4096;
    [SerializeField] int pointPerArea = 100;

    VisualEffect m_VfxInstance;
    Texture2D m_PositionMap, m_ColorMap;

    void Start()
    {
        m_VfxInstance = GetComponent<VisualEffect>();
    }

    void Update()
    {
        UpdateAttributeMaps();
    }

    void CreateAttributeMaps(int pointNum)
        {
            var texSize = Mathf.CeilToInt(Mathf.Sqrt(pointNum));

            m_PositionMap = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false);
            m_PositionMap.filterMode = FilterMode.Point;
            m_PositionMap.wrapMode = TextureWrapMode.Clamp;
            m_ColorMap = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false);
            m_ColorMap.filterMode = FilterMode.Point;
            m_ColorMap.wrapMode = TextureWrapMode.Clamp;

            m_VfxInstance.SetTexture("Position Map", m_PositionMap);
            m_VfxInstance.SetTexture("Color Map", m_ColorMap);
        }

    void UpdateAttributeMaps()
    {
        var mesh = meshPrefab.sharedMesh;
        mesh.SetVertices(mesh.vertices);
        mesh.SetIndices(mesh.triangles, MeshTopology.Triangles, 0, false);
        mesh.SetNormals(mesh.normals);

        var (positionList, colorList) = DividePolygon(mesh);

        if (m_PositionMap == null || m_PositionMap.width * m_PositionMap.height < positionList.Count)
        {
            CreateAttributeMaps(positionList.Count);
        }

        var bufSize = m_PositionMap.width * m_PositionMap.height;
        var posBuf = new Color[bufSize];
        var nrmBuf = new Color[bufSize];
        var colBuf = new Color[bufSize];
        for (var i = 0; i < positionList.Count; ++i)
        {
            var pos = positionList[i];
            var col = colorList[i];
            posBuf[i] = new Color(pos.x, pos.y, pos.z, 1.0f);
            // colBuf[i] = new Color(col.x, col.y, col.z, 0.0f); 
            colBuf[i] = new Color(1.0f, 1.0f, 1.0f, 0.0f); 
        }
        m_PositionMap.SetPixels(posBuf);
        m_PositionMap.Apply();
        m_ColorMap.SetPixels(colBuf);
        m_ColorMap.Apply();
    }

    (List<Vector3>, List<Vector3>) DividePolygon(Mesh mesh)
    {
        var triangles = mesh.triangles;
        var vertices = mesh.vertices;

        var posList = new List<Vector3>();
        var colorList = new List<Vector3>();

        for (var i = 0; i < triangles.Length; i += 3)
        {
            var idx0 = triangles[i];
            var idx1 = triangles[i + 1];
            var idx2 = triangles[i + 2];

            var pos0 = vertices[idx0];
            var pos1 = vertices[idx1];
            var pos2 = vertices[idx2];

            var col0 = new Vector3(0, 0, 0);
            var col1 = new Vector3(0, 0, 0);
            var col2 = new Vector3(0, 0, 0);

            var area = Vector3.Cross(pos1 - pos0, pos2 - pos0).magnitude * 0.5f;
            var pointNum = Math.Min(Mathf.CeilToInt(area * pointPerArea), maxPoints - posList.Count);

            for (var pIdx = 0; pIdx < pointNum; ++pIdx)
            {
                var weight0 = UnityEngine.Random.value;
                var weight1 = UnityEngine.Random.value;
                if (weight0 + weight1 > 1f)
                {
                    weight0 = 1f - weight0;
                    weight1 = 1f - weight1;
                }
                var weight2 = 1f - weight0 - weight1;

                var pos = pos0 * weight0 + pos1 * weight1 + pos2 * weight2;
                var col = col0 * weight0 + col1 * weight1 + col2 * weight2;

                posList.Add(pos);
                colorList.Add(col);
            }

            if (posList.Count > maxPoints)
            {
                break;
            }
        }

        return (posList, colorList);
    }
}