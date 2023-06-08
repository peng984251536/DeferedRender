using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


//unity提供的渲染pass的父类
public class TestRenderPass : ScriptableRenderPass
{
    private string m_ProfilerTag;

    //用于性能分析
    private ProfilingSampler m_ProfilingSampler;

    //用于覆盖渲染状态
    private RenderStateBlock m_RenderStateBlock;

    //渲染队列
    private RenderQueueType m_renderQueueType;

    //渲染时的过滤模式
    private FilteringSettings m_FilteringSettings;

    //覆盖的材质
    public Material overrideMaterial { get; set; }
    public int overrideMaterialPassIndex { get; set; }

    //创建该shader中各个pass的ShaderTagId
    private List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>()
    {
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("LightweightForward")
    };

    //TestRenderPass类的构造器，实例化的时候调用
    //Pass的构造方法，参数都由Feature传入
    //设置层级tag,性能分析的名字、渲染事件、过滤、队列、渲染覆盖设置等
    public TestRenderPass(string profilerTag, RenderPassEvent renderPassEvent,
        RenderObjects.FilterSettings filterSettings)
    {
        base.profilingSampler = new ProfilingSampler(nameof(TestRenderPass));
        m_ProfilerTag = profilerTag;
        m_ProfilingSampler = new ProfilingSampler(profilerTag);

        this.renderPassEvent = renderPassEvent;
        m_renderQueueType = filterSettings.RenderQueueType;
        RenderQueueRange renderQueueRange = (filterSettings.RenderQueueType == RenderQueueType.Transparent)
            ? RenderQueueRange.transparent
            : RenderQueueRange.opaque;
        uint renderingLayerMask = (uint) 1 << filterSettings.LayerMask - 1;
        m_FilteringSettings = new FilteringSettings(renderQueueRange, filterSettings.LayerMask, renderingLayerMask);

        m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
    }

    //设置深度状态
    public void SetDepthState(bool writeEnabled, CompareFunction function = CompareFunction.Less)
    {
        m_RenderStateBlock.mask |= RenderStateMask.Depth;
        m_RenderStateBlock.depthState = new DepthState(writeEnabled, function);
    }

    /// <summary>
    /// 最重要的方法，用来定义CommandBuffer并执行
    /// </summary>
    /// <param name="context"></param>
    /// <param name="renderingData"></param>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        SortingCriteria sortingCriteria = (m_renderQueueType == RenderQueueType.Transparent)
            ? SortingCriteria.CommonTransparent
            : renderingData.cameraData.defaultOpaqueSortFlags;

        //设置 渲染设置
        var drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
        drawingSettings.overrideMaterial = overrideMaterial;
        drawingSettings.overrideMaterialPassIndex = overrideMaterialPassIndex;
        
        //这里不需要所以没有直接写CommandBuffer，在下面Feature的AddRenderPasses加入了渲染队列，底层还是CB
        //发出渲染命令，内容包括制定的材质，还有材质的哪个pass
        //包括符合类型的，场景中的GameObject
        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
    }
}



