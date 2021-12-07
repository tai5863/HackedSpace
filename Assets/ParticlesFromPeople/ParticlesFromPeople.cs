using System.Collections.Generic;
using UnityEngine; 
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation; 
using UnityEngine.VFX; 

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(ARCameraBackground))] [RequireComponent(typeof(AROcclusionManager))] public class ParticlesFromPeople : MonoBehaviour
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
    VisualEffect m_VFXPrefab;
    [SerializeField]
    ComputeShader m_ComputeShader;
    ARCameraBackground m_ARCameraBackground;
    AROcclusionManager m_AROcclusionManager;
    Camera m_Camera;
    RenderTexture m_CaptureTexture, m_PositionTexture;
    VisualEffect m_VFXInstance;
    int m_Kernel;
    ThreadSize m_ThreadSize;

    void Awake()
    {
        // カメラ情報の取得
        m_Camera = GetComponent<Camera>();
        m_ARCameraBackground = GetComponent<ARCameraBackground>();
        m_AROcclusionManager = GetComponent<AROcclusionManager>();
    }

    void OnEnable()
    {
        // VFXグラフのインスタンスを作成
        m_VFXInstance = Instantiate(m_VFXPrefab, transform.parent);
        m_CaptureTexture = new RenderTexture(Screen.width, Screen.height, 16);
        m_CaptureTexture.Create();

        // コンピュートシェーダのセットアップ
        SetupComputeShader();
    }

    void onDisabe() {
        Destroy(m_VFXInstance);
        m_CaptureTexture.Release();
        m_PositionTexture.Release();
    }

    void Update() {
        Texture2D stencilTexture = m_AROcclusionManager.humanStencilTexture;
        Texture2D depthTexture = m_AROcclusionManager.humanDepthTexture;

        if (stencilTexture == null || depthTexture == null) {
            return;
        }

        Matrix4x4 invVPMatrix = (m_Camera.projectionMatrix * m_Camera.worldToCameraMatrix).inverse;

        m_ComputeShader.SetTexture(m_Kernel, "DepthTexture", depthTexture);
        m_ComputeShader.SetMatrix("InvVPMatrix", invVPMatrix);
        m_ComputeShader.SetMatrix("ProjectionMatrix", m_Camera.projectionMatrix);
        m_ComputeShader.Dispatch(m_Kernel, Mathf.CeilToInt(m_PositionTexture.width / m_ThreadSize.x), Mathf.CeilToInt(Screen.height / m_ThreadSize.y), 1);

        m_VFXInstance.SetTexture("Color Map", stencilTexture);
        m_VFXInstance.SetTexture("Position Map", m_PositionTexture);
    }
}
