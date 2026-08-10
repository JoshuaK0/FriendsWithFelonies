#pragma shader_feature _ SHADERGRAPH_PREVIEW

void RevealUsingColor_float(float3 WorldPosition, float4 Color, float Range, out float Out) {
    half4 Shadowmask = half4(1, 1, 1, 1);
    float totalAtten = 0;

#ifndef SHADERGRAPH_PREVIEW
    uint pixelLightCount = GetAdditionalLightsCount();

    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPosition;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
    float intensity = length(light.color.rgb);
    float atten = intensity * light.distanceAttenuation * light.shadowAttenuation;
    bool LightShouldReveal = distance(normalize(light.color.rgb), Color.rgb) < Range;
    totalAtten += LightShouldReveal ? atten : 0;
    LIGHT_LOOP_END
#endif

        Out = totalAtten;
}
