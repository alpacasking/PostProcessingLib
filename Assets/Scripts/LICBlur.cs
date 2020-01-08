using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

namespace AlpacasKing
{

    public enum ConvolutionType
    {
        DDA,
        LIC
    }

    [Serializable]
    public sealed class ConvolutionTypeParameter : ParameterOverride<ConvolutionType> { }


    [Serializable]
    [PostProcess(typeof(LineIntegralConvolutionRenderer), PostProcessEvent.AfterStack, "Alpacasking/Line Integral Convolution")]
    public sealed class LineIntegralConvolution : PostProcessEffectSettings
    {
        public ConvolutionTypeParameter Type = new ConvolutionTypeParameter { value = ConvolutionType.LIC };

        [Range(0, 10)]
        public IntParameter RelaxationIteration = new IntParameter { value = 2 };

        [Range(0f, 10f)]
        public FloatParameter RelaxationThreshold = new FloatParameter { value = 1f };

        [Range(0, 10)]
        public IntParameter ConvolutionIteration = new IntParameter { value = 1 };

        [Range(0, 10)]
        public IntParameter GaussianBlurWindowSize = new IntParameter { value = 4 };

        [Range(0.01f, 1f)]
        public FloatParameter GaussianBlurSigma = new FloatParameter { value = 0.5f };

        [Range(0, 10)]
        public IntParameter ConvolutionHalfLength = new IntParameter { value = 4 };

        [Range(0.01f, 1f)]
        public FloatParameter ConvolutionSigma = new FloatParameter { value = 0.1f };

        public override bool IsEnabledAndSupported(PostProcessRenderContext context)
        {
            return enabled.value;
        }
    }


    [UnityEngine.Scripting.Preserve]
    public sealed class LineIntegralConvolutionRenderer : PostProcessEffectRenderer<LineIntegralConvolution>
    {
        private ComputeShader structureTensorShader;

        private ComputeShader gaussianBlurShader;

        private ComputeShader LICBlurShader;

        private ComputeShader DDABlurShader;

        private ComputeShader grayScaleShader;

        private ComputeShader eigenShader;

        private int grayScaleKernelID;

        private int structureTensorKernelID;

        private int jacobiRelaxationKernelID;

        private int gaussianBlurHorizontalKernelID;

        private int gaussianBlurVerticalKernelID;

        private int LLICEularianKernelID;

        private int DDABlurKernelID;

        private int eigenKernelID;

        private int tempRTID1;

        private int tempRTID2;

        private int tempRTID3;
        public override void Init()
        {
            grayScaleShader = (ComputeShader)Resources.Load("GrayScale");
            structureTensorShader = (ComputeShader)Resources.Load("StructureTensor");
            gaussianBlurShader = (ComputeShader)Resources.Load("GaussianBlur");
            LICBlurShader = (ComputeShader)Resources.Load("LICBlur");
            DDABlurShader = (ComputeShader)Resources.Load("DDABlur");
            eigenShader = (ComputeShader)Resources.Load("Eigen");

            grayScaleKernelID = grayScaleShader.FindKernel("GrayScale");
            structureTensorKernelID = structureTensorShader.FindKernel("StructureTensor");
            jacobiRelaxationKernelID = structureTensorShader.FindKernel("JacobiRelaxation");
            gaussianBlurHorizontalKernelID = gaussianBlurShader.FindKernel("GaussianBlurHorizontal");
            gaussianBlurVerticalKernelID = gaussianBlurShader.FindKernel("GaussianBlurVertical");
            LLICEularianKernelID = LICBlurShader.FindKernel("LICEularian");
            DDABlurKernelID = DDABlurShader.FindKernel("DDABlur");
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
            cmd.BeginSample("LICBlur");
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

            // Jacobi Relaxation
            cmd.SetComputeFloatParam(structureTensorShader, "p", settings.RelaxationThreshold.value);
            for (int i = 0; i < settings.RelaxationIteration.value; i++)
            {
                cmd.SetComputeTextureParam(structureTensorShader, jacobiRelaxationKernelID, "Source", tempRTID2);
                cmd.SetComputeTextureParam(structureTensorShader, jacobiRelaxationKernelID, "Result", tempRTID1);

                cmd.DispatchCompute(structureTensorShader, jacobiRelaxationKernelID, context.width, context.height, 1);
                cmd.SetComputeTextureParam(structureTensorShader, jacobiRelaxationKernelID, "Source", tempRTID1);
                cmd.SetComputeTextureParam(structureTensorShader, jacobiRelaxationKernelID, "Result", tempRTID2);
                cmd.DispatchCompute(structureTensorShader, jacobiRelaxationKernelID, context.width, context.height, 1);
            }

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
            ComputeShader convolutionShader;
            int convolutionKernelID;
            switch (settings.Type.value)
            {
                case ConvolutionType.DDA:
                    convolutionShader = DDABlurShader;
                    convolutionKernelID = DDABlurKernelID;
                    break;
                case ConvolutionType.LIC:
                    convolutionShader = LICBlurShader;
                    convolutionKernelID = LLICEularianKernelID;
                    break;
                default:
                    convolutionShader = LICBlurShader;
                    convolutionKernelID = LLICEularianKernelID;
                    break;
            }
            cmd.Blit(context.source, tempRTID3);
            cmd.SetComputeIntParam(convolutionShader, "Length", settings.ConvolutionHalfLength.value);
            cmd.SetComputeFloatParam(convolutionShader, "sigma", settings.ConvolutionSigma.value);
            cmd.SetComputeIntParam(convolutionShader, "width", context.width);
            cmd.SetComputeIntParam(convolutionShader, "height", context.height);

            for (int i = 0; i < settings.ConvolutionIteration.value; i++)
            {

                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "Source", tempRTID3);
                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "VectorField", tempRTID1);
                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "Result", tempRTID2);
                cmd.DispatchCompute(convolutionShader, convolutionKernelID, context.width, context.height, 1);

                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "Source", tempRTID2);
                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "VectorField", tempRTID1);
                cmd.SetComputeTextureParam(convolutionShader, convolutionKernelID, "Result", tempRTID3);
                cmd.DispatchCompute(convolutionShader, convolutionKernelID, context.width, context.height, 1);
            }


            cmd.Blit(tempRTID3, context.destination);
            cmd.ReleaseTemporaryRT(tempRTID1);
            cmd.ReleaseTemporaryRT(tempRTID2);
            cmd.ReleaseTemporaryRT(tempRTID3);
            cmd.EndSample("LICBlur");
        }
    }
}