using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.VFX;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(ARCameraBackground))]
[RequireComponent(typeof(AROcclusionManager))]
public class ParticlesFromPeople : MonoBehaviour
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


        [SerializeField]
        VisualEffect m_VFXPrefab;
        [SerializeField]
        ComputeShader m_CS;

        ARCameraBackground m_ARCameraBackground;
        AROcclusionManager m_AROcclusionManager;
        Camera m_Camera;
        RenderTexture m_CaptureTexture, m_PositionTexture;
        VisualEffect m_VFXInstance;
        int m_Kernel;
        ThreadSize m_ThreadSize;
    }

    void Awake()
    {
        // カメラ情報の取得
        m_Camera = GetComponent<Camera>();
        m_ARCameraBackground = GetComponent<ARCameraBackground>();
        m_OcclusionManager = GetComponent<AROcclusionManager>();
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
}
