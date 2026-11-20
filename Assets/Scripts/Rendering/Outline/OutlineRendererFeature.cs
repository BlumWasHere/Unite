using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Copied from Kirby RPG
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        public Shader extrusionShader;
        public Shader compositeShader;
        public Shader blurShader;
        public RenderPassEvent captureEvent = RenderPassEvent.AfterRenderingTransparents;
        public RenderPassEvent compositeEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public GraphicsFormat outlineFormat = GraphicsFormat.R16G16B16A16_SFloat;
    }

    private static readonly int SourceTexId = Shader.PropertyToID("_SourceTex");
    private static readonly int SourceColorId = Shader.PropertyToID("_SourceColor");
    private static readonly int SourceTexStId = Shader.PropertyToID("_SourceTex_ST");
    private static readonly int OutlineSizeId = Shader.PropertyToID("_OutlineSize");
    private static readonly int OutlineConsistencyId = Shader.PropertyToID("_OutlineConsistency");
    private static readonly int OutlineSaturationId = Shader.PropertyToID("_OutlineSaturation");
    private static readonly int OutlineValueId = Shader.PropertyToID("_OutlineValue");
    private static readonly int OutlineTextureId = Shader.PropertyToID("_OutlineTexture");
    private static readonly int OutlineDepthTextureId = Shader.PropertyToID("_OutlineDepthTexture");
    private static readonly int OutlineScaleId = Shader.PropertyToID("_OutlineScale");
    private static readonly int OutlineSharpnessId = Shader.PropertyToID("_OutlineSharpness");
    private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
    private static readonly int BlurSpreadId = Shader.PropertyToID("_BlurSpread");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly ConcurrentDictionary<long, Material> extrusionMaterialCache = new ConcurrentDictionary<long, Material>();

    [SerializeField] private Settings settings = new Settings();

    private Material extrusionTemplate;
    private Material compositeMaterial;
    // Separate H/V instances: per-pass values are set at record time, so passes can't share one material.
    private Material blurMaterialH;
    private Material blurMaterialV;
    private RTHandle outlineColorHandle;
    private RTHandle outlineDepthHandle;
    private OutlineCapturePass capturePass;
    private OutlineCompositePass compositePass;
    private GlobalOutlineSettings.Values values;
    private OutlineCategory enabledCategories;

    public override void Create()
    {
        extrusionTemplate = CreateMaterial(settings.extrusionShader, "Hidden/Unite/OutlineExtrusion");
        compositeMaterial = CreateMaterial(settings.compositeShader, "Hidden/Unite/OutlineComposite");
        blurMaterialH = CreateMaterial(settings.blurShader, "Hidden/Unite/OutlineBlur");
        blurMaterialV = CreateMaterial(settings.blurShader, "Hidden/Unite/OutlineBlur");

        capturePass ??= new OutlineCapturePass();
        compositePass ??= new OutlineCompositePass();
        capturePass.renderPassEvent = settings.captureEvent;
        compositePass.renderPassEvent = settings.compositeEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game || renderingData.cameraData.camera == null)
            return;

        var target = renderingData.cameraData.camera.targetTexture;
        if (target != null && target.format == RenderTextureFormat.Depth)
            return;

        if (extrusionTemplate == null || compositeMaterial == null)
            return;

        GlobalOutlineSettings global = GlobalOutlineSettings.Instance;
        if (!global.enabled || global.enabledCategories == OutlineCategory.None)
            return;

        values = global.Resolve();
        enabledCategories = global.enabledCategories;

        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.depthBufferBits = 0;
        descriptor.graphicsFormat = settings.outlineFormat;
        RenderingUtils.ReAllocateHandleIfNeeded(ref outlineColorHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OutlineTexture");

        var depthDescriptor = descriptor;
        depthDescriptor.graphicsFormat = GraphicsFormat.None;
        depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
        depthDescriptor.depthBufferBits = 32;
        RenderingUtils.ReAllocateHandleIfNeeded(ref outlineDepthHandle, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_OutlineDepth");

        capturePass.Setup(this, outlineColorHandle, outlineDepthHandle);
        compositePass.Setup(this, compositeMaterial, blurMaterialH, blurMaterialV, outlineDepthHandle);

        renderer.EnqueuePass(capturePass);
        renderer.EnqueuePass(compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(extrusionTemplate);
        CoreUtils.Destroy(compositeMaterial);
        CoreUtils.Destroy(blurMaterialH);
        CoreUtils.Destroy(blurMaterialV);

        foreach (Material material in extrusionMaterialCache.Values)
            CoreUtils.Destroy(material);
        extrusionMaterialCache.Clear();

        outlineColorHandle?.Release();
        outlineDepthHandle?.Release();
        outlineColorHandle = null;
        outlineDepthHandle = null;
    }

    private static Material CreateMaterial(Shader shader, string fallbackShaderName)
    {
        shader ??= Shader.Find(fallbackShaderName);
        return shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
    }

    private static Texture ResolveSourceTexture(Material source, out Vector4 st)
    {
        st = new Vector4(1f, 1f, 0f, 0f);
        int[] ids = { BaseMapId, MainTexId };
        foreach (int id in ids)
        {
            if (!source.HasProperty(id)) continue;
            Texture texture = source.GetTexture(id);
            if (texture == null) continue;
            Vector2 scale = source.GetTextureScale(id);
            Vector2 offset = source.GetTextureOffset(id);
            st = new Vector4(scale.x, scale.y, offset.x, offset.y);
            return texture;
        }
        return null;
    }

    private static Color ResolveSourceColor(Material source, MaterialPropertyBlock block)
    {
        if (block != null)
        {
            if (block.HasColor(BaseColorId)) return block.GetColor(BaseColorId);
            if (block.HasColor(ColorId)) return block.GetColor(ColorId);
        }
        if (source.HasProperty(BaseColorId)) return source.GetColor(BaseColorId);
        if (source.HasProperty(ColorId)) return source.GetColor(ColorId);
        return Color.white;
    }

    private Material GetOrCreateExtrusionMaterial(Renderer renderer, Material source, int subMeshIndex, MaterialPropertyBlock block)
    {
        long key = ((long)renderer.GetInstanceID() << 32) ^ (uint)source.GetInstanceID() ^ (uint)subMeshIndex;
        if (!extrusionMaterialCache.TryGetValue(key, out Material outline) || outline == null)
        {
            outline = new Material(extrusionTemplate)
            {
                name = $"{source.name}_{renderer.GetInstanceID()}_{subMeshIndex}_Outline",
                hideFlags = HideFlags.HideAndDontSave
            };
            extrusionMaterialCache[key] = outline;
        }

        Texture texture = ResolveSourceTexture(source, out Vector4 st);
        outline.SetTexture(SourceTexId, texture != null ? texture : Texture2D.whiteTexture);
        outline.SetColor(SourceColorId, ResolveSourceColor(source, block));
        outline.SetVector(SourceTexStId, st);
        outline.SetFloat(OutlineSizeId, values.Size);
        outline.SetFloat(OutlineConsistencyId, values.Consistency);
        outline.SetFloat(OutlineSaturationId, values.Saturation);
        outline.SetFloat(OutlineValueId, values.Value);
        return outline;
    }

    private sealed class OutlineCapturePass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public OutlineRendererFeature feature;
        }

        private OutlineRendererFeature feature;
        private RTHandle colorHandle;
        private RTHandle depthHandle;

        public void Setup(OutlineRendererFeature owner, RTHandle color, RTHandle depth)
        {
            feature = owner;
            colorHandle = color;
            depthHandle = depth;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            TextureHandle color = renderGraph.ImportTexture(colorHandle);
            TextureHandle depth = renderGraph.ImportTexture(depthHandle);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineCapturePass", out var passData))
            {
                passData.feature = feature;
                builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(true, true, Color.clear);
                    var block = new MaterialPropertyBlock();

                    foreach (OutlineTag tag in OutlineTag.EnumerateActiveTags())
                    {
                        if (tag == null || !tag.isActiveAndEnabled || !tag.MatchesEnabledCategories(data.feature.enabledCategories))
                            continue;

                        IReadOnlyList<Renderer> renderers = tag.Renderers;
                        if (renderers == null) continue;

                        for (int i = 0; i < renderers.Count; i++)
                        {
                            Renderer renderer = renderers[i];
                            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                                continue;
                            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                                continue;

                            Material[] materials = renderer.sharedMaterials;
                            for (int sub = 0; sub < materials.Length; sub++)
                            {
                                if (materials[sub] == null) continue;
                                block.Clear();
                                renderer.GetPropertyBlock(block, sub);
                                Material outline = data.feature.GetOrCreateExtrusionMaterial(renderer, materials[sub], sub, block);
                                context.cmd.DrawRenderer(renderer, outline, sub, 0);
                            }
                        }
                    }
                });
            }
        }
    }

    private sealed class OutlineCompositePass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public OutlineRendererFeature feature;
            public Material material;
            public TextureHandle source;
            public TextureHandle outline;
        }

        private sealed class BlurPassData
        {
            public Material material;
            public TextureHandle source;
        }

        private OutlineRendererFeature feature;
        private Material material;
        private Material blurH;
        private Material blurV;
        private RTHandle outlineDepthHandle;
        private RTHandle outlineColorHandle;

        public void Setup(OutlineRendererFeature owner, Material composite, Material blurHorizontal, Material blurVertical, RTHandle outlineDepth)
        {
            feature = owner;
            material = composite;
            blurH = blurHorizontal;
            blurV = blurVertical;
            outlineDepthHandle = outlineDepth;
            outlineColorHandle = owner.outlineColorHandle;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        // Large radii come from iterating H+V passes with tap spacing capped at ~2 texels; spreading taps further ghosts.
        private TextureHandle AddBlurChain(RenderGraph renderGraph, TextureHandle outline)
        {
            float blur = feature.values.Blur;
            if (blur <= 0.01f || blurH == null || blurV == null)
                return outline;

            TextureDesc desc = renderGraph.GetTextureDesc(outline);
            float texelX = 1f / Mathf.Max(desc.width, 1);
            float texelY = 1f / Mathf.Max(desc.height, 1);

            int iterations = Mathf.Clamp(Mathf.CeilToInt(blur / 16f), 1, 16);
            float spread = Mathf.Clamp(blur / (iterations * 4f), 0.5f, 2f);

            blurH.SetVector(BlurDirectionId, new Vector4(texelX, 0f, 0f, 0f));
            blurH.SetFloat(BlurSpreadId, spread);
            blurV.SetVector(BlurDirectionId, new Vector4(0f, texelY, 0f, 0f));
            blurV.SetFloat(BlurSpreadId, spread);

            TextureDesc pingDesc = desc;
            pingDesc.name = "_OutlineBlurPing";
            pingDesc.clearBuffer = false;
            TextureDesc pongDesc = desc;
            pongDesc.name = "_OutlineBlurPong";
            pongDesc.clearBuffer = false;
            TextureHandle ping = renderGraph.CreateTexture(pingDesc);
            TextureHandle pong = renderGraph.CreateTexture(pongDesc);

            TextureHandle current = outline;
            for (int i = 0; i < iterations; i++)
            {
                AddBlurPass(renderGraph, current, ping, blurH, "OutlineBlurH");
                AddBlurPass(renderGraph, ping, pong, blurV, "OutlineBlurV");
                current = pong;
            }
            return current;
        }

        private static void AddBlurPass(RenderGraph renderGraph, TextureHandle source, TextureHandle destination, Material blurMaterial, string passName)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>(passName, out var passData))
            {
                passData.material = blurMaterial;
                passData.source = source;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (BlurPassData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0));
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle outline = AddBlurChain(renderGraph, renderGraph.ImportTexture(outlineColorHandle));
            TextureHandle outlineDepth = renderGraph.ImportTexture(outlineDepthHandle);

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "CameraColor-OutlineComposite";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("OutlineCompositePass", out var passData))
            {
                passData.feature = feature;
                passData.material = material;
                passData.source = source;
                passData.outline = outline;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(outline, AccessFlags.Read);
                builder.UseTexture(outlineDepth, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(OutlineTextureId, data.outline);
                    data.material.SetTexture(OutlineDepthTextureId, data.feature.outlineDepthHandle);
                    data.material.SetFloat(OutlineScaleId, data.feature.values.Scale);
                    data.material.SetFloat(OutlineSharpnessId, data.feature.values.Sharpness);
                    Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
                });
            }

            resourceData.cameraColor = destination;
        }
    }
}
