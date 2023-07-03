using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class TrmporalAAFeature : ScriptableRendererFeature
{
    #region Fields
    
    //CameraSettingPass m_cameraSettingPass;
    private TemporalAAPass m_TAAPass;
    private CameraSettingPass m_CameraSettingPass;

    public Shader taaShader;
    public bool OnCameraSetting = true;
    public bool OnTemporalAA = true;
    public MyTemporalAA.TAASetting setting;


    #endregion


    protected override void Dispose(bool disposing)
    {
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if(taaShader==null)
            return;
        if (m_TAAPass == null)
        {
            m_TAAPass = new TemporalAAPass("TemporalAAPass",
                taaShader,
                RenderPassEvent.BeforeRenderingPostProcessing);
        }

        if (m_CameraSettingPass == null)
        {
            m_CameraSettingPass = new CameraSettingPass();
        }
        if (!isActive)
            return;
        
        if(OnCameraSetting)
            renderer.EnqueuePass(m_CameraSettingPass);
        if(OnTemporalAA)
            renderer.EnqueuePass(m_TAAPass);
    }



    public override void Create()
    {
        name = "TAA";

        setting = MyTemporalAA.Instance.setting;
    }

    // void UpdateTAAData(RenderingData renderingData, TAAData TaaData, TemporalAntiAliasing Taa)
    // {
    //     Camera camera = renderingData.cameraData.camera;
    //     Vector2 additionalSample = Utils.GenerateRandomOffset2() * Taa.spread.value;
    //     TaaData.sampleOffset = additionalSample;
    //     TaaData.porjPreview = previewProj;
    //     TaaData.viewPreview = previewView;
    //     TaaData.projOverride = camera.orthographic
    //         ? Utils.GetJitteredOrthographicProjectionMatrix(camera, TaaData.sampleOffset)
    //         : Utils.GetJitteredPerspectiveProjectionMatrix(camera, TaaData.sampleOffset);
    //     TaaData.sampleOffset = new Vector2(TaaData.sampleOffset.x / camera.scaledPixelWidth,
    //         TaaData.sampleOffset.y / camera.scaledPixelHeight);
    //     previewView = camera.worldToCameraMatrix;
    //     previewProj = camera.projectionMatrix;
    //
    //     Debug.LogFormat("m_TaaData.additionalSample:{0}", TaaData.sampleOffset);
    // }
}