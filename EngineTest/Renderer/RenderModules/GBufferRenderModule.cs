﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeferredEngine.Recources;
using DeferredEngine.Renderer.Helper;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ShaderPlayground.Helpers;

namespace DeferredEngine.Renderer.RenderModules
{
    //Just a template
    public class GBufferRenderModule : IRenderModule
    {
        private Effect _clearShader;
        private Effect _gbufferShader;
        private EffectParameter _parameter1;
        private EffectPass _clearGBufferPass;

        private EffectParameter _WorldView;
        private EffectParameter _WorldViewProj;
        private EffectParameter _WorldViewIT;
        private EffectParameter _Camera;
        private EffectParameter _FarClip;

        private EffectParameter _Material_Metallic;
        private EffectParameter _Material_MetallicMap;
        private EffectParameter _Material_DiffuseColor;
        private EffectParameter _Material_Roughness;
        private EffectParameter _Material_MaskMap;
        private EffectParameter _Material_Texture;
        private EffectParameter _Material_NormalMap;
        private EffectParameter _Material_DisplacementMap;
        private EffectParameter _Material_RoughnessMap;
        private EffectParameter _Material_MaterialType;

        private EffectTechnique _DrawTextureDisplacement;
        private EffectTechnique _DrawTextureSpecularNormalMask;
        private EffectTechnique _DrawTextureNormalMask;
        private EffectTechnique _DrawTextureSpecularMask;
        private EffectTechnique _DrawTextureMask;
        private EffectTechnique _DrawTextureSpecularNormalMetallic;
        private EffectTechnique _DrawTextureSpecularNormal;
        private EffectTechnique _DrawTextureNormal;
        private EffectTechnique _DrawTextureSpecular;
        private EffectTechnique _DrawTextureSpecularMetallic;
        private EffectTechnique _DrawTexture;
        private EffectTechnique _DrawBasic;

        private FullScreenQuadRenderer _fsqRenderer;

        public GBufferRenderModule(ContentManager content, string shaderPathClear, string shaderPathGbuffer)
        {
            Load(content, shaderPathClear, shaderPathGbuffer);
        }

        private float _farClip;
        public float FarClip
        {
            get { return _farClip; }
            set
            {
                _farClip = value; 
                _FarClip.SetValue(value);
            }
        }

        private Vector3 _camera;
        public Vector3 Camera
        {
            get { return _camera; }
            set
            {
                _camera = value; 
                _Camera.SetValue(value);
            }
        }


        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _clearGBufferPass = _clearShader.Techniques["Clear"].Passes[0];

            _fsqRenderer = new FullScreenQuadRenderer(graphicsDevice);
            
            _WorldView = _gbufferShader.Parameters["WorldView"];
            _WorldViewProj = _gbufferShader.Parameters["WorldViewProj"];
            _WorldViewIT = _gbufferShader.Parameters["WorldViewIT"];
            _Camera = _gbufferShader.Parameters["Camera"];
            _FarClip = _gbufferShader.Parameters["FarClip"];

            _Material_Metallic = _gbufferShader.Parameters["Metallic"];
            _Material_MetallicMap = _gbufferShader.Parameters["MetallicMap"];
            _Material_DiffuseColor = _gbufferShader.Parameters["DiffuseColor"];
            _Material_Roughness = _gbufferShader.Parameters["Roughness"];

            _Material_MaskMap = _gbufferShader.Parameters["Mask"];
            _Material_Texture = _gbufferShader.Parameters["Texture"];
            _Material_NormalMap = _gbufferShader.Parameters["NormalMap"];
            _Material_RoughnessMap = _gbufferShader.Parameters["RoughnessMap"];
            _Material_DisplacementMap = _gbufferShader.Parameters["DisplacementMap"];

            _Material_MaterialType = _gbufferShader.Parameters["MaterialType"];

            //Techniques

            _DrawTextureDisplacement = _gbufferShader.Techniques["DrawTextureDisplacement"];
            _DrawTextureSpecularNormalMask = _gbufferShader.Techniques["DrawTextureSpecularNormalMask"];
            _DrawTextureNormalMask = _gbufferShader.Techniques["DrawTextureNormalMask"];
            _DrawTextureSpecularMask = _gbufferShader.Techniques["DrawTextureSpecularMask"];
            _DrawTextureMask = _gbufferShader.Techniques["DrawTextureMask"];
            _DrawTextureSpecularNormalMetallic = _gbufferShader.Techniques["DrawTextureSpecularNormalMetallic"];
            _DrawTextureSpecularNormal = _gbufferShader.Techniques["DrawTextureSpecularNormal"];
            _DrawTextureNormal = _gbufferShader.Techniques["DrawTextureNormal"];
            _DrawTextureSpecular = _gbufferShader.Techniques["DrawTextureSpecular"];
            _DrawTextureSpecularMetallic = _gbufferShader.Techniques["DrawTextureSpecularMetallic"];
            _DrawTexture = _gbufferShader.Techniques["DrawTexture"];
            _DrawBasic = _gbufferShader.Techniques["DrawBasic"];
        }
        
        public void Load(ContentManager content, string shaderPath, string shaderPathGbuffer)
        {
            _clearShader = content.Load<Effect>(shaderPath);
            _gbufferShader = content.Load<Effect>(shaderPathGbuffer);
        }

        public void Draw(GraphicsDevice _graphicsDevice, RenderTargetBinding[] _renderTargetBinding, MeshMaterialLibrary meshMaterialLibrary, Matrix _viewProjection, Matrix _view)
        {
            _graphicsDevice.SetRenderTargets(_renderTargetBinding);

            //Clear the GBuffer
            if (GameSettings.g_ClearGBuffer)
            {
                _graphicsDevice.RasterizerState = RasterizerState.CullNone;
                _graphicsDevice.BlendState = BlendState.Opaque;
                _graphicsDevice.DepthStencilState = DepthStencilState.Default;

                _clearGBufferPass.Apply();
                _fsqRenderer.RenderFullscreenQuad(_graphicsDevice);
            }

            //Draw the Gbuffer!

            meshMaterialLibrary.Draw(renderType: MeshMaterialLibrary.RenderType.Opaque, viewProjection: _viewProjection, lightViewPointChanged: true, view: _view, renderModule: this);

        }

        public void Apply(Matrix localWorldMatrix, Matrix? view, Matrix viewProjection)
        {
            Matrix worldView = localWorldMatrix * (Matrix)view;
            _WorldView.SetValue(worldView);
            _WorldViewProj.SetValue(localWorldMatrix * viewProjection);

            worldView = Matrix.Transpose(Matrix.Invert(worldView));
            _WorldViewIT.SetValue(worldView);

            _gbufferShader.CurrentTechnique.Passes[0].Apply();
        }

        public void SetMaterialSettings(MaterialEffect material)
        {
            if (GameSettings.d_defaultMaterial)
            {
                _Material_DiffuseColor.SetValue(Color.Gray.ToVector3());
                _Material_Roughness.SetValue(GameSettings.m_defaultRoughness > 0
                        ? GameSettings.m_defaultRoughness
                        : 0.3f);
                _Material_Metallic.SetValue(0.0f);
                _Material_MaterialType.SetValue(0);
                _gbufferShader.CurrentTechnique = _DrawBasic;
            }
            else
            {
                if (material.HasDisplacement)
                {
                    _Material_Texture.SetValue(material.AlbedoMap);
                    _Material_NormalMap.SetValue(material.NormalMap);
                    _Material_DisplacementMap.SetValue(material.DisplacementMap);
                    _gbufferShader.CurrentTechnique =
                        _DrawTextureDisplacement;
                }
                else if (material.HasMask) //Has diffuse for sure then
                {
                    if (material.HasNormalMap && material.HasRoughnessMap)
                    {
                        _Material_MaskMap.SetValue(material.Mask);
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_NormalMap.SetValue(material.NormalMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularNormalMask;
                    }

                    else if (material.HasNormalMap)
                    {
                        _Material_MaskMap.SetValue(material.Mask);
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_NormalMap.SetValue(material.NormalMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureNormalMask;
                    }

                    else if (material.HasRoughnessMap)
                    {
                        _Material_MaskMap.SetValue(material.Mask);
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularMask;
                    }
                    else
                    {
                        _Material_MaskMap.SetValue(material.Mask);
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularMask;
                    }
                }
                else
                {
                    if (material.HasNormalMap && material.HasRoughnessMap && material.HasDiffuse &&
                        material.HasMetallic)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_NormalMap.SetValue(material.NormalMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _Material_MetallicMap.SetValue(material.MetallicMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularNormalMetallic;
                    }

                    else if (material.HasNormalMap && material.HasRoughnessMap && material.HasDiffuse)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_NormalMap.SetValue(material.NormalMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularNormal;
                    }

                    else if (material.HasNormalMap && material.HasDiffuse)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_NormalMap.SetValue(material.NormalMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureNormal;
                    }

                    else if (material.HasMetallic && material.HasRoughnessMap && material.HasDiffuse)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecularMetallic;
                    }

                    else if (material.HasRoughnessMap && material.HasDiffuse)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _Material_RoughnessMap.SetValue(material.RoughnessMap);
                        _gbufferShader.CurrentTechnique =
                            _DrawTextureSpecular;
                    }

                    else if (material.HasDiffuse)
                    {
                        _Material_Texture.SetValue(material.AlbedoMap);
                        _gbufferShader.CurrentTechnique = _DrawTexture;
                    }

                    else
                    {
                        _gbufferShader.CurrentTechnique = _DrawBasic;
                    }
                }


                if (!material.HasDiffuse)
                {
                    if (material.Type == MaterialEffect.MaterialTypes.Emissive && material.EmissiveStrength > 0)
                    {
                        _Material_DiffuseColor.SetValue(material.DiffuseColor);
                        _Material_Metallic.SetValue(material.EmissiveStrength / 8);
                    }
                    //* Math.Max(material.EmissiveStrength,1));
                    //}
                    else
                        //{
                        _Material_DiffuseColor.SetValue(material.DiffuseColor);
                    //}
                }

                if (!material.HasRoughnessMap)
                    _Material_Roughness.SetValue(GameSettings.m_defaultRoughness >
                                                                               0
                        ? GameSettings.m_defaultRoughness
                        : material.Roughness);
                _Material_Metallic.SetValue(material.Metallic);
                _Material_MaterialType.SetValue(material.MaterialTypeNumber);
            }
        }
    }
}