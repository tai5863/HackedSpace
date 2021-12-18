using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.VFX;

[ExecuteInEditMode]
[RequireComponent(typeof(ARMeshManager))]
public class ARParticlesFromMesh : MonoBehaviour
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

    [SerializeField] VisualEffect m_VfxPrefab;
    [SerializeField] int m_MaxPoints = 4096;
    [SerializeField] int m_PointPerArea = 100;

    ARMeshManager m_MeshManager;
    MeshFilter m_MeshPrefab;
     Camera m_Camera;
    ARCameraManager m_CameraManager;
    VisualEffect m_VfxInstance;
    Texture2D m_PositionMap, m_NormalMap, m_ColorMap, m_CameraFeedTexture;

    void Awake()
    {
        m_MeshManager = GetComponent<ARMeshManager>();
        m_Camera = (Camera)FindObjectOfType(typeof(Camera));
        m_CameraManager = (ARCameraManager)FindObjectOfType(typeof(ARCameraManager));
    }

    void OnEnable() { 
        Debug.Assert(m_MeshManager != null, "mesh manager cannot be null");
        m_CameraManager.frameReceived += OnCameraFrameReceived;
        m_VfxInstance = Instantiate(m_VfxPrefab, transform.parent);
    }

    void OnDisable()
    {
        Debug.Assert(m_MeshManager != null, "mesh manager cannot be null");
        m_CameraManager.frameReceived -= OnCameraFrameReceived;
        DestroyImmediate(m_VfxInstance);
    }

    void OnUpdated(ARMeshesChangedEventArgs eventArgs)
    {
        m_VfxInstance.SetVector3("Transform_position", transform.position);
        m_VfxInstance.SetVector3("Transform_angles", transform.eulerAngles);
        m_VfxInstance.SetVector3("Transform_scale", transform.lossyScale);
        UpdateVfxAttributeMaps();
    }

    void CreateVfxAttributeMaps(int pointNum)
    {
        var texSize = Mathf.CeilToInt(Mathf.Sqrt(pointNum));

        m_PositionMap = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false);
        m_PositionMap.filterMode = FilterMode.Point;
        m_PositionMap.wrapMode = TextureWrapMode.Clamp;
        m_NormalMap = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false);
        m_NormalMap.filterMode = FilterMode.Point;
        m_NormalMap.wrapMode = TextureWrapMode.Clamp;
        m_ColorMap = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false);
        m_ColorMap.filterMode = FilterMode.Point;
        m_ColorMap.wrapMode = TextureWrapMode.Clamp;

        m_VfxInstance.SetTexture("Position Map", m_PositionMap);
        m_VfxInstance.SetTexture("Normal Map", m_NormalMap);
        m_VfxInstance.SetTexture("Color Map", m_ColorMap);
    }

    void UpdateVfxAttributeMaps()
    {
        var mesh = new Mesh();
        m_MeshPrefab = m_MeshManager.meshPrefab;
        mesh.SetVertices(m_MeshPrefab.mesh.vertices);
        mesh.SetIndices(m_MeshPrefab.mesh.triangles, MeshTopology.Triangles, 0, false);
        mesh.SetNormals(m_MeshPrefab.mesh.normals);

        var (positionList, normalList, colorList) = DividePolygon(mesh);

        if (m_PositionMap == null || m_PositionMap.width * m_PositionMap.height < positionList.Count)
        {
            CreateVfxAttributeMaps(positionList.Count);
        }

        var bufSize = m_PositionMap.width * m_NormalMap.height;
        var posBuf = new Color[bufSize];
        var nrmBuf = new Color[bufSize];
        var colBuf = new Color[bufSize];
        for (var i = 0; i < positionList.Count; ++i)
        {
            var pos = positionList[i];
            var nrm = normalList[i];
            var col = colorList[i];
            posBuf[i] = new Color(pos.x, pos.y, pos.z, 1.0f);
            nrmBuf[i] = new Color(nrm.x, nrm.y, nrm.z, 0.0f);
            colBuf[i] = new Color(col.x, col.y, col.z, 0.0f); 
        }
        m_PositionMap.SetPixels(posBuf);
        m_PositionMap.Apply();
        m_NormalMap.SetPixels(nrmBuf);
        m_NormalMap.Apply();
        m_ColorMap.SetPixels(colBuf);
        m_ColorMap.Apply();
    }

    (List<Vector3>, List<Vector3>, List<Vector3>) DividePolygon(Mesh mesh)
    {
        var triangles = mesh.triangles;
        var vertices = mesh.vertices;
        var normals = mesh.normals;

        var posList = new List<Vector3>();
        var normalList = new List<Vector3>();
        var colorList = new List<Vector3>();

        for (var i = 0; i < triangles.Length; i += 3)
        {
            var idx0 = triangles[i];
            var idx1 = triangles[i + 1];
            var idx2 = triangles[i + 2];

            var pos0 = vertices[idx0];
            var pos1 = vertices[idx1];
            var pos2 = vertices[idx2];

            var nrm0 = normals[idx0];
            var nrm1 = normals[idx1];
            var nrm2 = normals[idx2];

            var col0 = SampleCameraFeedTexture(pos0);
            var col1 = SampleCameraFeedTexture(pos1);
            var col2 = SampleCameraFeedTexture(pos2);

            var area = Vector3.Cross(pos1 - pos0, pos2 - pos0).magnitude * 0.5f;
            var pointNum = Math.Min(Mathf.CeilToInt(area * m_PointPerArea), m_MaxPoints - posList.Count);

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
                var nrm = nrm0 * weight0 + nrm1 * weight1 + nrm2 * weight2;
                var col = col0 * weight0 + col1 * weight1 + col2 * weight2;

                posList.Add(pos);
                normalList.Add(nrm);
                colorList.Add(col);
            }

            if (posList.Count > m_MaxPoints)
            {
                break;
            }
        }

        return (posList, normalList, colorList);
    }
    Vector3 SampleCameraFeedTexture(Vector3 facePosition)
    {
        if (m_CameraFeedTexture == null)
        {
            return Vector3.zero;
        }
        var viewPosition = m_Camera.WorldToViewportPoint(transform.TransformPoint(facePosition));
        if (Input.deviceOrientation == DeviceOrientation.Portrait)
        {
            viewPosition = new Vector3(1.0f - viewPosition.y, viewPosition.x, viewPosition.z);
        }
        else if (Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown)
        {
            viewPosition = new Vector3(viewPosition.y, 1.0f - viewPosition.x, viewPosition.z);
        }
        Color color = m_CameraFeedTexture.GetPixelBilinear(viewPosition.x, viewPosition.y).linear;
        return new Vector3(color.r, color.g, color.b);
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        XRCpuImage cameraImage;
        if (!m_CameraManager.TryAcquireLatestCpuImage(out cameraImage)) return;

        if (m_CameraFeedTexture == null || m_CameraFeedTexture.width != cameraImage.width || m_CameraFeedTexture.height != cameraImage.height)
        {
            m_CameraFeedTexture = new Texture2D(cameraImage.width, cameraImage.height, TextureFormat.RGBA32, false);
        }

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cameraImage.width, cameraImage.height),
            outputDimensions = new Vector2Int(cameraImage.width / 2, cameraImage.height / 2),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        int size = cameraImage.GetConvertedDataSize(conversionParams);

        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        cameraImage.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);

        cameraImage.Dispose();

        m_CameraFeedTexture = new Texture2D(
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y,
            conversionParams.outputFormat,
            false
        );

        m_CameraFeedTexture.LoadRawTextureData(buffer);
        m_CameraFeedTexture.Apply();

        buffer.Dispose();
    }
}