using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class CameraSettingPass : ScriptableRenderPass
{
    string m_ProfilerTag = "SetCameraPass";

    //TAAData m_TaaData;
    internal CameraSettingPass()
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        profilingSampler = new ProfilingSampler(m_ProfilerTag);
    }

    /// <summary>
    /// 相机渲染前执行
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="renderingData"></param>
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {

        
    }

    /// <summary>
    /// 渲染过程的执行逻辑
    /// </summary>
    /// <param name="context"></param>
    /// <param name="renderingData"></param>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, profilingSampler))
        {
            cmd.Clear();
            Matrix4x4 projOverride = GetProMatrix4x4(renderingData);
            CameraData cameraData = renderingData.cameraData;
            cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, projOverride);
            //Debug.Log(string.Format("X:{0} y:{1}",projOverride.m02,projOverride.m12));
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    
    public Matrix4x4 GetProMatrix4x4(RenderingData renderingData)
    {
        CameraData cameraData = renderingData.cameraData;
        Camera camera = cameraData.camera;
        Vector2 offset = MyTemporalAA.CalculateJitterMatrix(ref cameraData);
        Matrix4x4 projectionMatrix = camera.projectionMatrix;
        projectionMatrix.m02 = offset.x;
        projectionMatrix.m12 = offset.y;
        // cameraData.SetViewProjectionAndJitterMatrix
        //     (camera.worldToCameraMatrix, projectionMatrix,Matrix4x4.identity);

        //Matrix4x4 test = cameraData.GetProjectionMatrix();
        //Debug.Log(string.Format("X:{0} y:{1}",projectionMatrix.m02,projectionMatrix.m12));
        return projectionMatrix;
        
    }
}