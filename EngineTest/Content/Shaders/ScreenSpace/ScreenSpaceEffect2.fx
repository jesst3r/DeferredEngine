﻿
#include "helper.fx"

float4x4 ViewProjection;
float4x4 InverseViewProjection;
float3 CameraPosition;

bool AccuracyMode=false;

Texture2D DepthMap;
Texture2D TargetMap;
Texture2D NormalMap;
//SamplerState texSampler
//{
//    Texture = (AlbedoMap);
//    AddressU = CLAMP;
//    AddressV = CLAMP;
//    MagFilter = POINT;
//    MinFilter = POINT;
//    Mipfilter = POINT;
//};
SamplerState texSampler
{
    Texture = (AlbedoMap);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
};
 
float zfar = 500;
float znear = 1;

float2 resolution = float2(1280, 800);

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  STRUCT DEFINITIONS

struct VertexShaderInput
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 viewDirWS : TEXCOORD1;
    float3 viewDirVS : TEXCOORD2;
};

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  FUNCTION DEFINITIONS

 //  DEFAULT LIGHT SHADER FOR MODELS
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = float4(input.Position, 1);
    //align texture coordinates
    output.TexCoord = input.TexCoord;
    output.viewDirVS = input.Position.xyz;
    output.viewDirWS = normalize(mul(normalize(output.Position), InverseViewProjection).xyz);
    return output;

}

float lD(float z)
{
    float zfar_2 = zfar / (zfar - znear);

    float z0 = z * zfar_2 - znear * zfar_2;

    float w0 = z;

    float native_z = z0 / w0;

    float linZ = (znear * zfar_2 / (zfar_2 - native_z));

    return linZ;
}



float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 output = float4(0, 0, 0, 0);

    float4 positionVS;
    positionVS.x = input.TexCoord.x * 2.0f - 1.0f;
    positionVS.y = -(input.TexCoord.y * 2.0f - 1.0f);

    float2 texCoord = float2(input.TexCoord);

    float depthVal = 1 - DepthMap.Sample(texSampler, texCoord).r;

    float4 normalData = NormalMap.Sample(texSampler, texCoord);
	//tranform normal back into [-1,1] range
    float3 normal = decode(normalData.xyz);
    float roughness = normalData.a;


    [branch]
    if (normalData.x + normalData.y <= 0.001f || roughness > 0.8f) //Out of range
    {
        return float4(0, 0, 0, 0);
    }

	////compute screen-space position

	//linDepth
	//float linDepth = 1 + (Projection._43 / (depthVal - Projection._33));

	//RealSpace
    positionVS.w = 1.0f;
    positionVS.z = depthVal;
    float4 positionWS = mul(positionVS, InverseViewProjection);
    positionWS /= positionWS.w;

	// float3 incident = normalize(input.viewDirWS);
    float3 incident = normalize(positionWS.xyz - CameraPosition);

    float3 reflectVector = reflect(incident, normal);
	// go

    float4 samplePositionVS = mul(positionWS + float4(reflectVector, 0), ViewProjection);
    samplePositionVS /= samplePositionVS.w;

    float4 Offset = (samplePositionVS - positionVS);

    float xMultiplier = 0;
    float yMultiplier = 0;
            //Lets go to the end of the screen
    if (Offset.x > 0)
    {
        xMultiplier = (1 - positionVS.x) / Offset.x;
    }
    else
    {
        xMultiplier = (-1 - positionVS.x) / Offset.x;
    }

    if (Offset.y > 0)
    {
        yMultiplier = (1 - positionVS.y) / Offset.y;
    }
    else
    {
        yMultiplier = (-1 - positionVS.y) / Offset.y;
    }

    //what multiplier is smaller?

    float multiplier = min(xMultiplier, yMultiplier); //xMultiplier < yMultiplier ? xMultiplier : yMultiplier;

    //int samples = 20;

    //Offset *= multiplier / samples;

    Offset *= multiplier;

    float maxOffset = max(abs(Offset.x), abs(Offset.y));
           
    static int samples = 15; //int(maxOffset * 20);
    
    static float border = 0.1f;

    static float border2 = 1 - border;
    static float bordermulti = 1 / border;

    Offset /= samples;

    float startingDepth = samplePositionVS.z;

    float4 hitPosition;

	[unroll]
    for (int i = 0; i < samples; i++)
    {
		//if (i >= samples)
		//	break;

        if (Offset.z < 0)
            break;

        float4 rayPositionVS = samplePositionVS + Offset * i;

        //float2 sampleTexCoord = 0.5f * (float2(samplePositionVS.x, -samplePositionVS.y) + 1);

        float2 sampleTexCoord = 0.5f * (float2(rayPositionVS.x, -rayPositionVS.y) + float2(1, 1));

        float depthValRay = 1 - DepthMap.Sample(texSampler, sampleTexCoord).r;
          
        //We have a hit
        [branch]     
        if (depthValRay <= rayPositionVS.z) //&& (Offset.z > 0)) //&& depthValRay >= startingDepth)  ) //|| (Offset.z < 0 && depthValRay < startingDepth)))
        {
            hitPosition = rayPositionVS;

            bool hit = false;
            
            int samples2 = samples + 1 - i; //samples + 1 - i;

            float depthValRayPrevious = depthValRay;
            float4 rayPositionVSPrevious = rayPositionVS;

            //Let's go backwards now and check when we are no longer behind something
            [branch]
            for (int j = 1; j <= samples2; j++)
            {
                rayPositionVS = hitPosition - Offset * j / (samples2);

                float2 sampleTexCoordAccurate = 0.5f * (float2(rayPositionVS.x, -rayPositionVS.y) + float2(1, 1));

                int3 texCoordInt2 = int3(sampleTexCoordAccurate * resolution, 0);
                depthValRay = 1 - DepthMap.Load(texCoordInt2).r;

                //Looks like we don't hit anything any more?
                [branch]
                if (depthValRay > rayPositionVS.z) 
                {
                    //if (!AccuracyMode)
                    //{
                    //    sampleTexCoord = sampleTexCoordAccurate;
                    //}
                    //else
                    //{
                        //lin interpolate

                        //only z is relevant
                        float prevZ = 0; // rayPositionVSPrevious.z;
                        float d = rayPositionVSPrevious.z - rayPositionVS.z;

                        float /*depthPrev*/ b = depthValRayPrevious - rayPositionVS.z;
                        float /*depth*/ a = depthValRay - rayPositionVS.z;

                        float x = -b / (b - a) * (1 / (-d - 1 / (b - a))) / d;

                        float2 sampleTexCoordPrevious = 0.5f * (float2(rayPositionVSPrevious.x, -rayPositionVSPrevious.y) + float2(1, 1));

                        float depthValRayLerped = lerp(depthValRay, depthValRayPrevious, x);

                        hit = true;

                        sampleTexCoord = lerp(sampleTexCoordAccurate, sampleTexCoordPrevious, x);

                        if (depthValRayLerped >= hitPosition.z)
                        {
                            hit = false;
                            //sampleTexCoord = sampleTexCoordAccurate;
                        }
                        //else
                        //{
                        //    //depthValRay = depthValRayLerped;
                        //}

                        //if(depthValRay > rayPositionPrevious.z)
                        //    hit = false;
                    //}
                    break;
                }

                depthValRayPrevious = depthValRay;
                rayPositionVSPrevious = rayPositionVS;
            }
            
            if (!hit)
                continue;

            //if (depthValRay < startingDepth)
            //    break;

            int3 texCoordInt = int3(sampleTexCoord * resolution, 0);
            float4 albedoColor = TargetMap.Load(texCoordInt);

            //float4 albedoColor = TargetMap.SampleLevel(texSampler, sampleTexCoord, roughness*5*i);

            output = albedoColor;
            output.a = 1;

            [branch]
            if (sampleTexCoord.y > border2)
            {
                output.a = lerp(1, 0, (sampleTexCoord.y - border2) * bordermulti);
            }
            else if (sampleTexCoord.y < border)
            {
                output.a = lerp(0, 1, sampleTexCoord.y * bordermulti);
            }
            [branch]
            if (sampleTexCoord.x > border2)
            {
                output.a *= lerp(1, 0, (sampleTexCoord.x - border2) * bordermulti);
            }
            else if (sampleTexCoord.x < border)
            {
                output.a *= lerp(0, 1, sampleTexCoord.x * bordermulti);
            }
            
            output.rgb *= output.a * (1 - roughness);
			
            break;
        }

        startingDepth = rayPositionVS.z;
    }

    return output;
}


float4 PixelShaderFunction2(VertexShaderOutput input) : COLOR0
{
    float4 output = float4(0, 0, 0, 0);
    float4 positionVS;
    positionVS.x = input.TexCoord.x * 2.0f - 1.0f;
    positionVS.y = -(input.TexCoord.y * 2.0f - 1.0f);
    float2 texCoord = float2(input.TexCoord);
    float depthVal = 1 - DepthMap.Sample(texSampler, texCoord).r;
    float4 normalData = NormalMap.Sample(texSampler, texCoord);
    float3 normal = decode(normalData.xyz);
    float roughness = normalData.a;
    [branch]
    if (normalData.x + normalData.y <= 0.001f || roughness > 0.8f) //Out of range
    {
        return float4(0, 0, 0, 0);
    }
    positionVS.w = 1.0f;
    positionVS.z = depthVal;
    float4 positionWS = mul(positionVS, InverseViewProjection);
    positionWS /= positionWS.w;
    float3 incident = normalize(positionWS.xyz - CameraPosition);
    float3 reflectVector = reflect(incident, normal);
    float4 samplePositionVS = mul(positionWS + float4(reflectVector, 0), ViewProjection);
    samplePositionVS /= samplePositionVS.w;
    float4 Offset = (samplePositionVS - positionVS);
    float xMultiplier = 0;
    float yMultiplier = 0;
    if (Offset.x > 0)
    {
        xMultiplier = (1 - positionVS.x) / Offset.x;
    }
    else
    {
        xMultiplier = (-1 - positionVS.x) / Offset.x;
    }

    if (Offset.y > 0)
    {
        yMultiplier = (1 - positionVS.y) / Offset.y;
    }
    else
    {
        yMultiplier = (-1 - positionVS.y) / Offset.y;
    }
    float multiplier = min(xMultiplier, yMultiplier);
    Offset *= multiplier;
    float maxOffset = max(abs(Offset.x), abs(Offset.y));
    static int samples = 15;
    static float border = 0.1f;
    static float border2 = 1 - border;
    static float bordermulti = 1 / border;
    Offset /= samples;
    float startingDepth = samplePositionVS.z;
    float4 hitPosition;
	[branch]
    for (int i = 0; i < samples; i++)
    {
        if (Offset.z < 0) break;
        float4 rayPositionVS = samplePositionVS + Offset * i;
        float2 sampleTexCoord = 0.5f * (float2(rayPositionVS.x, -rayPositionVS.y) + float2(1, 1));
        float depthValRay = 1 - DepthMap.Sample(texSampler, sampleTexCoord).r;
        [branch]     
        if (depthValRay <= rayPositionVS.z && (Offset.z > 0)) //&& depthValRay >= startingDepth)  ) //|| (Offset.z < 0 && depthValRay < startingDepth)))
        {
            hitPosition = rayPositionVS;
            
                int samples2 = samples + 3 - i;
                [branch]
                for (int j = samples2; j > 0; j--)
                {
                    rayPositionVS = hitPosition - Offset * j / (samples2 + 1);
                    float2 sampleTexCoordAccurate = 0.5f * (float2(rayPositionVS.x, -rayPositionVS.y) + float2(1, 1));
                    int3 texCoordInt2 = int3(sampleTexCoordAccurate * resolution, 0);
                    depthValRay = 1 - DepthMap.Load(texCoordInt2).r;
                    if (depthValRay < rayPositionVS.z && depthValRay >= startingDepth)
                    {
                        sampleTexCoord = sampleTexCoordAccurate;
                        break;
                    }
                }
            if (depthValRay < startingDepth)
                break;
            int3 texCoordInt = int3(sampleTexCoord * resolution, 0);
            float4 albedoColor = TargetMap.Load(texCoordInt);
            output = albedoColor;
            output.a = 1;
            [branch]
            if (sampleTexCoord.y > border2)
            {
                output.a = lerp(1, 0, (sampleTexCoord.y - border2) * bordermulti);
            }
            else if (sampleTexCoord.y < border)
            {
                output.a = lerp(0, 1, sampleTexCoord.y * bordermulti);
            }
            [branch]
            if (sampleTexCoord.x > border2)
            {
                output.a *= lerp(1, 0, (sampleTexCoord.x - border2) * bordermulti);
            }
            else if (sampleTexCoord.x < border)
            {
                output.a *= lerp(0, 1, sampleTexCoord.x * bordermulti);
            }
            output.rgb *= output.a * (1 - roughness);
            break;
        }
        startingDepth = rayPositionVS.z;
    }
    return output;
}

technique Default
{
    pass Pass1
    {
        VertexShader = compile vs_5_0 VertexShaderFunction();
        PixelShader = compile ps_5_0 PixelShaderFunction();
    }
}

technique Old
{
    pass Pass1
    {
        VertexShader = compile vs_5_0 VertexShaderFunction();
        PixelShader = compile ps_5_0 PixelShaderFunction2();
    }
}


