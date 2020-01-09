using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

namespace AlpacasKing
{

    [Serializable]
    [PostProcess(typeof(StylizedEdgeRenderer), PostProcessEvent.AfterStack, "Alpacasking/Stylized Edge")]
    public sealed class StylizedEdge : PostProcessEffectSettings
    {
        [Tooltip("Stroke texture used to paint."), DisplayName("Stroke")]
        public TextureParameter strokeTexture = new TextureParameter { value = null };

        [Range(0, 10)]
        public IntParameter GaussianBlurWindowSize = new IntParameter { value = 4 };

        [Range(0.01f, 1f)]
        public FloatParameter GaussianBlurSigma = new FloatParameter { value = 0.5f };

        [Range(0, 10)]
        public IntParameter ConvolutionHalfLength = new IntParameter { value = 4 };

        [Range(0f, 10f)]
        public FloatParameter ConvolutionStep = new FloatParameter { value = 1f };

        [Range(0.01f, 1f)]
        public FloatParameter ConvolutionSigma = new FloatParameter { value = 0.1f };

        [Range(0f, 1f)]
        public FloatParameter EdgeThreshold = new FloatParameter { value = 0f };

        public override bool IsEnabledAndSupported(PostProcessRenderContext context)
        {
            return enabled.value && strokeTexture != null;
        }
    }


    [UnityEngine.Scripting.Preserve]
    public sealed class StylizedEdgeRenderer : PostProcessEffectRenderer<StylizedEdge>
    {
        private ComputeShader structureTensorShader;

        private ComputeShader gaussianBlurShader;

        private ComputeShader edgeLICShader;

        private ComputeShader grayScaleShader;

        private ComputeShader eigenShader;

        private int grayScaleKernelID;

        private int structureTensorKernelID;

        private int gaussianBlurHorizontalKernelID;

        private int gaussianBlurVerticalKernelID;

        private int LLICEularianKernelID;

        private int eigenKernelID;

        private int tempRTID1;

        private int tempRTID2;

        private int tempRTID3;
        public override void Init()
        {
            grayScaleShader = (ComputeShader)Resources.Load("GrayScale");
            structureTensorShader = (ComputeShader)Resources.Load("StructureTensor");
            gaussianBlurShader = (ComputeShader)Resources.Load("GaussianBlur");
            edgeLICShader = (ComputeShader)Resources.Load("EdgeLIC");
            eigenShader = (ComputeShader)Resources.Load("Eigen");

            grayScaleKernelID = grayScaleShader.FindKernel("GrayScale");
            structureTensorKernelID = structureTensorShader.FindKernel("StructureTensor");
            gaussianBlurHorizontalKernelID = gaussianBlurShader.FindKernel("GaussianBlurHorizontal");
            gaussianBlurVerticalKernelID = gaussianBlurShader.FindKernel("GaussianBlurVertical");
            LLICEularianKernelID = edgeLICShader.FindKernel("LICEularian");
            eigenKernelID = eigenShader.FindKernel("Eigen");


            tempRTID1 = Shader.PropertyToID("tempRTID1");
            tempRTID2 = Shader.PropertyToID("tempRTID2");
            tempRTID3 = Shader.PropertyToID("tempRTID3");
        }
        public override void Release()
        {

        }

        public override void Render(PostProcessRenderContext context)
        {
            var cmd = context.command;
            cmd.BeginSample("StylizedEdge");
            RenderTextureDescriptor desc = new RenderTextureDescriptor(context.width, context.height);
            desc.enableRandomWrite = true;
            desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
            cmd.GetTemporaryRT(tempRTID1, desc);
            cmd.GetTemporaryRT(tempRTID2, desc);
            cmd.GetTemporaryRT(tempRTID3, desc);
            //To GrayScale
            cmd.SetComputeTextureParam(grayScaleShader, grayScaleKernelID, "Source", context.source);
            cmd.SetComputeTextureParam(grayScaleShader, grayScaleKernelID, "Result", tempRTID1);
            cmd.DispatchCompute(grayScaleShader, grayScaleKernelID, context.width, context.height, 1);

            // Structure Tensor
            cmd.SetComputeIntParam(structureTensorShader, "width", context.width);
            cmd.SetComputeIntParam(structureTensorShader, "height", context.height);
            cmd.SetComputeTextureParam(structureTensorShader, structureTensorKernelID, "Source", tempRTID1);
            cmd.SetComputeTextureParam(structureTensorShader, structureTensorKernelID, "Result", tempRTID2);
            cmd.SetComputeFloatParam(structureTensorShader, "p", 0.183f);
            cmd.DispatchCompute(structureTensorShader, structureTensorKernelID, context.width, context.height, 1);

            // Blur Tensor
            cmd.SetComputeIntParam(gaussianBlurShader, "width", context.width);
            cmd.SetComputeIntParam(gaussianBlurShader, "height", context.height);
            cmd.SetComputeIntParam(gaussianBlurShader, "WindowSize", settings.GaussianBlurWindowSize.value);
            cmd.SetComputeFloatParam(gaussianBlurShader, "Sigma", settings.GaussianBlurSigma.value);
            cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurHorizontalKernelID, "Source", tempRTID2);
            cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurHorizontalKernelID, "Result", tempRTID1);
            cmd.DispatchCompute(gaussianBlurShader, gaussianBlurHorizontalKernelID, context.width, context.height, 1);
            cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurVerticalKernelID, "Source", tempRTID1);
            cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurVerticalKernelID, "Result", tempRTID2);
            cmd.DispatchCompute(gaussianBlurShader, gaussianBlurVerticalKernelID, context.width, context.height, 1);

            // eigen
            cmd.SetComputeTextureParam(eigenShader, eigenKernelID, "Source", tempRTID2);
            cmd.SetComputeTextureParam(eigenShader, eigenKernelID, "Result", tempRTID1);
            cmd.DispatchCompute(eigenShader, eigenKernelID, context.width, context.height, 1);

            // LIC
            var strokeTex = settings.strokeTexture.value;
            cmd.Blit(strokeTex, tempRTID3,new Vector2(context.width/ (float)strokeTex.width, context.height / (float)strokeTex.height),new Vector2(0,0));
            cmd.SetComputeIntParam(edgeLICShader, "Length", settings.ConvolutionHalfLength.value);
            cmd.SetComputeFloatParam(edgeLICShader, "sigma", settings.ConvolutionSigma.value);
            cmd.SetComputeIntParam(edgeLICShader, "width", context.width);
            cmd.SetComputeIntParam(edgeLICShader, "height", context.height);
            cmd.SetComputeFloatParam(edgeLICShader, "step", settings.ConvolutionStep.value);
            cmd.SetComputeFloatParam(edgeLICShader, "edgeThreshold", settings.EdgeThreshold.value);
            cmd.SetComputeTextureParam(edgeLICShader, LLICEularianKernelID, "Source", tempRTID3);
            cmd.SetComputeTextureParam(edgeLICShader, LLICEularianKernelID, "VectorField", tempRTID1);
            cmd.SetComputeTextureParam(edgeLICShader, LLICEularianKernelID, "Result", tempRTID2);
            cmd.DispatchCompute(edgeLICShader, LLICEularianKernelID, context.width, context.height, 1);

            cmd.Blit(tempRTID2, context.destination);
            cmd.ReleaseTemporaryRT(tempRTID1);
            cmd.ReleaseTemporaryRT(tempRTID2);
            cmd.ReleaseTemporaryRT(tempRTID3);
            cmd.EndSample("StylizedEdge");
        }
    }
}