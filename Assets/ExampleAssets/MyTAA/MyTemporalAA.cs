using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using URPRendering = UnityEngine.Rendering.Universal;


public class MyTemporalAA
{
    public enum TemporalAAQuality
    {
        VeryLow,
        Low,
        Medium,
        High,
        VeryHigh,
    }
    
    [System.Serializable]
    public class TAASetting
    {
        public TemporalAAQuality quality;
        [Range(0,1)]
        public float frameInfluence;
        public float jitterScale;
        public float mipBias;
        public float varianceClampScale;
        public float contrastAdaptiveSharpening;

        [NonSerialized] public int resetHistoryFrames;

        public TAASetting()
        {
            quality                    = TemporalAAQuality.High;
            frameInfluence             = 0.1f;
            jitterScale                = 1.0f;
            mipBias                    = 0.0f;
            varianceClampScale         = 0.9f;
            contrastAdaptiveSharpening = 0.0f; // Disabled

            resetHistoryFrames = 0;
        }
    }
    
    private static MyTemporalAA instance;

    public static MyTemporalAA Instance
    {
        get
        {
            if (instance == null)
                instance = new MyTemporalAA();
            return instance;
        }
    }

    private MyTemporalAA()
    {
        setting = new TAASetting();
    }

    public int frameCount = 0;
    private int _LastAccumUpdateFrameIndex = 0;
    public int LastAccumUpdateFrameIndex
    {
        get { return _LastAccumUpdateFrameIndex; }
        set { _LastAccumUpdateFrameIndex = value; }
    }

    public TAASetting setting;

    static public Vector2 CalculateJitterMatrix(ref CameraData cameraData)
    {
        Matrix4x4 jitterMat = Matrix4x4.identity;
        Vector2 offset = Vector2.zero;

        //bool is_Jitter = TemporalAAPass;
        bool isJitter = MyTemporalAA.Instance.IsMyTemporalAAEnabled(cameraData);
        if (isJitter)
        {
            int taaFrameIndex = Application.isPlaying ? Time.frameCount : MyTemporalAA.Instance.frameCount++;

            float actualWidth = cameraData.cameraTargetDescriptor.width;
            float actualHeight = cameraData.cameraTargetDescriptor.height;
            float jitterScale = 1.0f;

            var jitter = CalculateJitter(taaFrameIndex) * jitterScale;

            float offsetX = jitter.x * (2.0f / actualWidth);
            float offsetY = jitter.y * (2.0f / actualHeight);

            jitterMat = Matrix4x4.Translate(new Vector3(offsetX, offsetY, 0.0f));
            offset.x = offsetX;
            offset.y = offsetY;

            //Debug.Log("FrameCount:"+taaFrameIndex);
            //Debug.LogFormat("偏移向量：{0},{1}",jitter.x,jitter.y);
        }

        return offset;
    }

    static internal Vector2 CalculateJitter(int frameIndex)
    {
        // The variance between 0 and the actual halton sequence values reveals noticeable
        // instability in Unity's shadow maps, so we avoid index 0.
        float jitterX = HaltonSequence.Get((frameIndex & 1023) + 1, 2) - 0.5f;
        float jitterY = HaltonSequence.Get((frameIndex & 1023) + 1, 3) - 0.5f;

        return new Vector2(jitterX, jitterY);
    }

    private static readonly Vector2[] taaFilterOffsets = new Vector2[]
    {
        new Vector2(0.0f, 0.0f),

        new Vector2(0.0f, 1.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(-1.0f, 0.0f),
        new Vector2(0.0f, -1.0f),

        new Vector2(-1.0f, 1.0f),
        new Vector2(1.0f, -1.0f),
        new Vector2(1.0f, 1.0f),
        new Vector2(-1.0f, -1.0f)
    };

    private static readonly float[] taaFilterWeights = new float[taaFilterOffsets.Length + 1];
    public float[] CalculateFilterWeights(float jitterScale)
    {
        // Based on HDRP
        // Precompute weights used for the Blackman-Harris filter.
        float totalWeight = 0;
        for (int i = 0; i < 9; ++i)
        {
            Vector2 jitter = CalculateJitter(Time.frameCount) * jitterScale;
            // The rendered frame (pixel grid) is already jittered.
            // We sample 3x3 neighbors with int offsets, but weight the samples
            // relative to the distance to the non-jittered pixel center.
            // From the POV of offset[0] at (0,0), the original pixel center is at (-jitter.x, -jitter.y).
            float x = taaFilterOffsets[i].x - jitter.x;
            float y = taaFilterOffsets[i].y - jitter.y;
            float d2 = (x * x + y * y);

            taaFilterWeights[i] = Mathf.Exp((-0.5f / (0.22f)) * d2);
            totalWeight += taaFilterWeights[i];
        }

        // Normalize weights.
        for (int i = 0; i < 9; ++i)
        {
            taaFilterWeights[i] /= totalWeight;
        }

        return taaFilterWeights;
    }

    public bool IsMyTemporalAAEnabled(CameraData cameraData)
    {
        UniversalAdditionalCameraData additionalCameraData;
        cameraData.camera.TryGetComponent(out additionalCameraData);

        return (cameraData.cameraTargetDescriptor.msaaSamples == 1) // No MSAA
               && !(additionalCameraData?.renderType == CameraRenderType.Overlay ||
                    additionalCameraData?.cameraStack.Count > 0) // No Camera stack
               && !cameraData.camera.allowDynamicResolution; // No Postprocessing
    }
}