using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
[Serializable]
[PostProcess(typeof(ColorTransferRenderer), PostProcessEvent.AfterStack, "Alpacasking/ColorTransfer")]
public sealed class ColorTransfer : PostProcessEffectSettings
{
    [Tooltip("Target texture."), DisplayName("Target")]
    public TextureParameter targetTexture = new TextureParameter { value = null };
    [Range(1, 100), Tooltip("Effect strength."), DisplayName("Strength")]
    public FloatParameter Strength = new FloatParameter { value = 10 };
    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return enabled.value
            && targetTexture.value != null;
    }
}


[UnityEngine.Scripting.Preserve]
public sealed class ColorTransferRenderer : PostProcessEffectRenderer<ColorTransfer>
{
    private ComputeShader colorSpaceComputeShader;

    private ComputeShader statisticsShader;

    private ComputeShader colorTransferShader;

    private ComputeBuffer srcStatisticsBuffer;
    private ComputeBuffer tarStatisticsBuffer;
    private int rgbToLabKernelID;

    private int labToRgbKernelID;

    private int sumKernelID;

    private int SDSumKernelID;

    private int averageSumKernelID;

    private int averageSDSumKernelID;

    private int colorTransferKernelID;
    private int tempRTID1;

    private int tempRTID2;

    private int tempRTID3;

    private int[] startData = { 0, 0, 0, 0, 0, 0 };

    private int[] temptData = { 0, 0, 0, 0, 0, 0 };
    public override void Init()
    {
        colorSpaceComputeShader = (ComputeShader)Resources.Load("ColorSpace");
        statisticsShader = (ComputeShader)Resources.Load("Statistics");
        colorTransferShader = (ComputeShader)Resources.Load("ColorTransfer");

        rgbToLabKernelID = colorSpaceComputeShader.FindKernel("RgbToLab");
        labToRgbKernelID = colorSpaceComputeShader.FindKernel("LabToRgb");

        sumKernelID = statisticsShader.FindKernel("Sum");
        SDSumKernelID = statisticsShader.FindKernel("SDSum");
        averageSumKernelID = statisticsShader.FindKernel("AverageSum");
        averageSDSumKernelID = statisticsShader.FindKernel("AverageSDSum");
        colorTransferKernelID = colorTransferShader.FindKernel("ColorTransfer");

        srcStatisticsBuffer = new ComputeBuffer(6, sizeof(int));
        srcStatisticsBuffer.SetData(startData);


        tarStatisticsBuffer = new ComputeBuffer(6, sizeof(int));
        tarStatisticsBuffer.SetData(startData);

        tempRTID1 = Shader.PropertyToID("tempRTID1");
        tempRTID2 = Shader.PropertyToID("tempRTID2");
        tempRTID3 = Shader.PropertyToID("tempRTID3");

    }
    public override void Release()
    {
        if (srcStatisticsBuffer != null)
        {
            srcStatisticsBuffer.Release();
        }
        if (tarStatisticsBuffer != null)
        {
            tarStatisticsBuffer.Release();
        }
    }

    public override void Render(PostProcessRenderContext context)
    {
        if (srcStatisticsBuffer == null)
        {
            Init();
        }
        int srcWidth = context.width;
        int srcHeight = context.height;
        int tarWidth = settings.targetTexture.value.width;
        int tarHeight = settings.targetTexture.value.height;
        /*srcStatisticsBuffer.GetData(temptData);
        for (int i = 0; i < 6; i++)
        {
            Debug.Log(i + "," + temptData[i]);
        }*/
        srcStatisticsBuffer.SetData(startData);
        tarStatisticsBuffer.SetData(startData);

        var cmd = context.command;
        cmd.BeginSample("ColorTransfer");
        RenderTextureDescriptor desc = new RenderTextureDescriptor(srcWidth, srcHeight);
        desc.enableRandomWrite = true;
        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
        cmd.GetTemporaryRT(tempRTID1, desc);
        cmd.GetTemporaryRT(tempRTID3, desc);
        desc.width = tarWidth;
        desc.height = tarHeight;
        cmd.GetTemporaryRT(tempRTID2, desc);
        // source:rgb->lab
        cmd.SetComputeFloatParam(colorSpaceComputeShader, "Strength", settings.Strength.value);
        cmd.SetComputeTextureParam(colorSpaceComputeShader, rgbToLabKernelID, "Source", context.source);
        cmd.SetComputeTextureParam(colorSpaceComputeShader, rgbToLabKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(colorSpaceComputeShader, rgbToLabKernelID, srcWidth, srcHeight, 1);

        //source:statistics
        cmd.SetComputeTextureParam(statisticsShader, sumKernelID, "Source", tempRTID1);
        cmd.SetComputeBufferParam(statisticsShader, sumKernelID, "StatisticsBuffer", srcStatisticsBuffer);
        cmd.DispatchCompute(statisticsShader, sumKernelID, srcWidth, srcHeight, 1);

        cmd.SetComputeBufferParam(statisticsShader, averageSumKernelID, "StatisticsBuffer", srcStatisticsBuffer);
        cmd.SetComputeIntParam(statisticsShader, "Num", srcWidth * srcHeight);
        cmd.DispatchCompute(statisticsShader, averageSumKernelID, 1, 1, 1);

        cmd.SetComputeTextureParam(statisticsShader, SDSumKernelID, "Source", tempRTID1);
        cmd.SetComputeBufferParam(statisticsShader, SDSumKernelID, "StatisticsBuffer", srcStatisticsBuffer);
        cmd.DispatchCompute(statisticsShader, SDSumKernelID, srcWidth, srcHeight, 1);

        cmd.SetComputeBufferParam(statisticsShader, averageSDSumKernelID, "StatisticsBuffer", srcStatisticsBuffer);
        cmd.SetComputeIntParam(statisticsShader, "Num", srcWidth * srcHeight);
        cmd.DispatchCompute(statisticsShader, averageSDSumKernelID, 1, 1, 1);


        // target:rgb->lab
        cmd.SetComputeTextureParam(colorSpaceComputeShader, rgbToLabKernelID, "Source", settings.targetTexture.value);
        cmd.SetComputeTextureParam(colorSpaceComputeShader, rgbToLabKernelID, "Result", tempRTID2);
        cmd.DispatchCompute(colorSpaceComputeShader, rgbToLabKernelID, tarWidth, tarHeight, 1);

        // target:statistics
        cmd.SetComputeTextureParam(statisticsShader, sumKernelID, "Source", tempRTID2);
        cmd.SetComputeBufferParam(statisticsShader, sumKernelID, "StatisticsBuffer", tarStatisticsBuffer);
        cmd.DispatchCompute(statisticsShader, sumKernelID, tarWidth, tarHeight, 1);

        cmd.SetComputeBufferParam(statisticsShader, averageSumKernelID, "StatisticsBuffer", tarStatisticsBuffer);
        cmd.SetComputeIntParam(statisticsShader, "Num", tarWidth * tarHeight);
        cmd.DispatchCompute(statisticsShader, averageSumKernelID, 1, 1, 1);

        cmd.SetComputeTextureParam(statisticsShader, SDSumKernelID, "Source", tempRTID2);
        cmd.SetComputeBufferParam(statisticsShader, SDSumKernelID, "StatisticsBuffer", tarStatisticsBuffer);
        cmd.DispatchCompute(statisticsShader, SDSumKernelID, tarWidth, tarHeight, 1);

        cmd.SetComputeBufferParam(statisticsShader, averageSDSumKernelID, "StatisticsBuffer", tarStatisticsBuffer);
        cmd.SetComputeIntParam(statisticsShader, "Num", tarWidth * tarHeight);
        cmd.DispatchCompute(statisticsShader, averageSDSumKernelID, 1, 1, 1);


        // color transfer
        cmd.SetComputeTextureParam(colorTransferShader, colorTransferKernelID, "Source", tempRTID1);
        cmd.SetComputeTextureParam(colorTransferShader, colorTransferKernelID, "Result", tempRTID3);
        cmd.SetComputeBufferParam(colorTransferShader, colorTransferKernelID, "SrcStatisticsBuffer", srcStatisticsBuffer);
        cmd.SetComputeBufferParam(colorTransferShader, colorTransferKernelID, "TarStatisticsBuffer", tarStatisticsBuffer);
        cmd.DispatchCompute(colorTransferShader, colorTransferKernelID, srcWidth, srcHeight, 1);

        // lab->rgb
        cmd.SetComputeTextureParam(colorSpaceComputeShader, labToRgbKernelID, "Source", tempRTID3);
        cmd.SetComputeTextureParam(colorSpaceComputeShader, labToRgbKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(colorSpaceComputeShader, labToRgbKernelID, srcWidth, srcHeight, 1);


        cmd.Blit(tempRTID1, context.destination);

        cmd.ReleaseTemporaryRT(tempRTID1);
        cmd.ReleaseTemporaryRT(tempRTID2);
        cmd.ReleaseTemporaryRT(tempRTID3);
        cmd.EndSample("ColorTransfer");
    }
}