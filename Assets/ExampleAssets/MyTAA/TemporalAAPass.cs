using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class TemporalAAPass : ScriptableRenderPass
    {
        private string m_ProfilerTag;

        //用于性能分析
        private ProfilingSampler m_ProfilingSampler;

        //渲染队列
        private RenderQueueType m_renderQueueType;

        //渲染时的过滤模式
        //private FilteringSettings m_FilteringSettings;

        //覆盖的材质
        public Material taaMaterial { get; set; }

        //历史帧
        public RTHandle HistoryAccumulationTex;
        public RTHandle HistoryAccumulationTex2;

        public static readonly int _TaaAccumulationTex = Shader.PropertyToID("_TaaAccumulationTex");
        public static readonly int _TaaMotionVectorTex = Shader.PropertyToID("_TaaMotionVectorTex");

        public static readonly int _TaaFilterWeights = Shader.PropertyToID("_TaaFilterWeights");

        public static readonly int _TaaFrameInfluence = Shader.PropertyToID("_TaaFrameInfluence");
        public static readonly int _TaaVarianceClampScale = Shader.PropertyToID("_TaaVarianceClampScale");

        //TestRenderPass类的构造器，实例化的时候调用
        //Pass的构造方法，参数都由Feature传入
        //设置层级tag,性能分析的名字、渲染事件、过滤、队列、渲染覆盖设置等
        public TemporalAAPass(string profilerTag,Shader shader ,RenderPassEvent renderPassEvent)
        {
            //m_ProfilerTag = profilerTag;
            profilingSampler = new ProfilingSampler(profilerTag);

            this.renderPassEvent = renderPassEvent;
            if (shader == null)
            {
                Debug.LogErrorFormat($"Missing shader. {GetType().DeclaringType.Name} render pass will not execute. Check for missing reference in the renderer resources.");
            }
            taaMaterial = CoreUtils.CreateEngineMaterial(shader);
            // m_renderQueueType = filterSettings.RenderQueueType;
            // RenderQueueRange renderQueueRange = (filterSettings.RenderQueueType == RenderQueueType.Transparent)
            //     ? RenderQueueRange.transparent
            //     : RenderQueueRange.opaque;
            // uint renderingLayerMask = (uint) 1 << filterSettings.LayerMask - 1;
            // m_FilteringSettings = new FilteringSettings(renderQueueRange, filterSettings.LayerMask, renderingLayerMask);
            //
            // m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            if (HistoryAccumulationTex == null || HistoryAccumulationTex2 == null)
            {
                const bool enableRandomWrite = false;

                desc.msaaSamples = 1;
                desc.mipCount = 0;
                desc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                desc.sRGB = false;
                desc.depthBufferBits = 0;
                desc.memoryless = RenderTextureMemoryless.None;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                desc.enableRandomWrite = enableRandomWrite;
                desc.bindMS = false;
                desc.useDynamicScale = false;


                HistoryAccumulationTex =
                    RTHandles.Alloc(desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_TaaAccumulationTex");
                HistoryAccumulationTex2 =
                    RTHandles.Alloc(desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_TaaAccumulationTex2");
            }
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
        }

        /// <summary>
        /// 最重要的方法，用来定义CommandBuffer并执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="renderingData"></param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                RTHandle source = HistoryAccumulationTex2;
                // RTHandle destination = RTHandles.Alloc(cameraData.cameraTargetDescriptor, FilterMode.Bilinear,
                //     TextureWrapMode.Clamp,
                //     name: "_DesTex");
                RTHandle cameraRT = cameraData.renderer.cameraColorTargetHandle;

                int frameCount = Application.isPlaying ? Time.frameCount : MyTemporalAA.Instance.frameCount;
                bool isNewFrame = MyTemporalAA.Instance.LastAccumUpdateFrameIndex != frameCount;

                RTHandle taaHistoryAccumulationTex = this.HistoryAccumulationTex;
                taaMaterial.SetTexture(_TaaAccumulationTex, taaHistoryAccumulationTex);

                // RenderTargetIdentifier motionVectors = new RenderTargetIdentifier("_MotionVectorTexture");
                // taaMaterial.SetTexture(_TaaMotionVectorTex,
                //     isNewFrame ? motionVectors : Texture2D.blackTexture);

                var taa = MyTemporalAA.Instance.setting;
                float taaInfluence = taa.resetHistoryFrames == 0 ? taa.frameInfluence : 1.0f;
                taaMaterial.SetFloat(_TaaFrameInfluence, taaInfluence);
                taaMaterial.SetFloat(_TaaVarianceClampScale, taa.varianceClampScale);

                if (taa.quality == MyTemporalAA.TemporalAAQuality.VeryHigh)
                    taaMaterial.SetFloatArray(_TaaFilterWeights,
                        MyTemporalAA.Instance.CalculateFilterWeights(taa.jitterScale));

                cmd.Blit(cameraRT,source);
                Blitter.BlitCameraTexture(cmd, source, cameraRT, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, taaMaterial, (int) taa.quality);

                //复制历史帧
                if (isNewFrame)
                {
                    int kHistoryCopyPass = taaMaterial.shader.passCount - 1;
                    Blitter.BlitCameraTexture(cmd, cameraRT, taaHistoryAccumulationTex,
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, taaMaterial, kHistoryCopyPass);
                    MyTemporalAA.Instance.LastAccumUpdateFrameIndex = frameCount;
                }
                
                //Blitter.BlitTexture(cmd,cameraRT,taaHistoryAccumulationTex,material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

//unity提供的渲染pass的父类