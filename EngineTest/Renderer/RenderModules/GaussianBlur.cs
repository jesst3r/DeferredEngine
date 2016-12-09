﻿using System;
using EngineTest.Recources;
using EngineTest.Renderer.Helper;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EngineTest.Renderer.RenderModules
{
    public class GaussianBlur
    {
        private GraphicsDevice _graphicsDevice;

        private Effect _gaussEffect;
        private EffectPass _horizontalPass;
        private EffectPass _verticalPass;

        private RenderTarget2D _rt2562;
        private RenderTarget2D _rt5122;
        private RenderTarget2D _rt10242;
        private RenderTarget2D _rt20482;

        private QuadRenderer _quadRenderer;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
           _graphicsDevice = graphicsDevice;
            _gaussEffect = Shaders.GaussianBlurEffect;

            _quadRenderer = new QuadRenderer();

            _horizontalPass = _gaussEffect.Techniques["GaussianBlur"].Passes["Horizontal"];
            _verticalPass = _gaussEffect.Techniques["GaussianBlur"].Passes["Vertical"];

            _rt2562 = new RenderTarget2D(graphicsDevice, 256, 256, false, SurfaceFormat.Vector2, DepthFormat.None);
            _rt5122 = new RenderTarget2D(graphicsDevice, 512,512, false, SurfaceFormat.Vector2, DepthFormat.None);
            _rt10242 = new RenderTarget2D(graphicsDevice, 1024,1024, false, SurfaceFormat.Vector2, DepthFormat.None);
            _rt20482 = new RenderTarget2D(graphicsDevice, 2048,2048, false, SurfaceFormat.Vector2, DepthFormat.None);
        }

        public void Dispose()
        {
            _rt2562.Dispose();
            _rt5122.Dispose();
            _rt10242.Dispose();
            _rt20482.Dispose();
        }

        public RenderTarget2D DrawGaussianBlur(RenderTarget2D renderTargetOutput)
        {
            //select rendertarget
            RenderTarget2D renderTargetBlur = null;

            if (renderTargetOutput.Format != SurfaceFormat.Vector2)
                throw new NotImplementedException("Unsupported Format for blurring");

            //Only square expected
            int size = renderTargetOutput.Width;
            switch (size)
            {
                case 256:
                    renderTargetBlur = _rt2562;
                    break;
                case 512:
                    renderTargetBlur = _rt5122;
                    break;
                case 1024:
                    renderTargetBlur = _rt10242;
                    break;
                case 2048:
                    renderTargetBlur = _rt20482;
                    break;
            }

            if(renderTargetBlur == null)
                throw new NotImplementedException("Unsupported Size for blurring");

            _graphicsDevice.SetRenderTarget(renderTargetBlur);

            Vector2 invRes = new Vector2(1.0f/size, 1.0f/size);
            Shaders.GaussianBlurEffectParameter_InverseResolution.SetValue(invRes);
            Shaders.GaussianBlurEffectParameter_TargetMap.SetValue(renderTargetOutput);

            _horizontalPass.Apply();
            _quadRenderer.RenderQuad(_graphicsDevice, Vector2.One * -1, Vector2.One);

            _graphicsDevice.SetRenderTarget(renderTargetOutput);
            Shaders.GaussianBlurEffectParameter_TargetMap.SetValue(renderTargetBlur);
            _verticalPass.Apply();
            _quadRenderer.RenderQuad(_graphicsDevice, Vector2.One * -1, Vector2.One);

            return renderTargetOutput;
        }
    }
}
