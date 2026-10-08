using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// D-08: the one Renderer Feature of the look — pen outline (depth + normals prepass) and watercolour wash in a single full-screen
    /// pass after transparents (so billboard sprites are inked and washed together with the world). URP 17 Render Graph only.
    /// Settings: <see cref="WatercolorSettings"/>; wired into the renderer by Editor/LookSetup, not by hand.
    /// </summary>
    public sealed class WatercolorFeature : ScriptableRendererFeature
    {
        [SerializeField] private WatercolorSettings _settings;
        [SerializeField] private Shader _shader;

        private Material _material;
        private Texture2D _paper;
        private WatercolorPass _pass;
        private OverlayPass _overlay;
        private bool _warned;

        /// <summary>D-26: the world's colour multiplier from the hero's state (1 = untouched, 0.75 = −25 % when she is badly hurt); set by CombatFeel, multiplies the grade's saturation.</summary>
        public static float StateSaturation { get; set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => StateSaturation = 1f;

        /// <summary>Settings asset (the editor tools flip <see cref="WatercolorSettings.enabled"/> for the comparison frame).</summary>
        public WatercolorSettings Settings => _settings;

        public void Configure(WatercolorSettings settings, Shader shader)
        {
            _settings = settings;
            _shader = shader;
        }

        public override void Create()
        {
            _pass = new WatercolorPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents, requiresIntermediateTexture = true };
            _overlay = new OverlayPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            renderer.EnqueuePass(_overlay); // D-21: always, wash or not — materials whose pass is ZdAfterWash are drawn only here
            if (_settings == null || !_settings.enabled) return;
            if (!EnsureResources()) return;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            _pass.Setup(_material, _settings, _paper);
            renderer.EnqueuePass(_pass);
        }

        private bool EnsureResources()
        {
            if (_shader == null) _shader = Shader.Find("Hidden/Zelda/Watercolor");
            if (_shader == null || !_shader.isSupported)
            {
                if (!_warned) { ZdLog.Warn("Look", "watercolour shader missing or unsupported — no effect"); _warned = true; }
                return false;
            }
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(_shader);
            if (_paper == null) _paper = PaperTexture.Create();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_paper);
            _material = null;
            _paper = null;
        }

        /// <summary>
        /// D-21: draws the renderers whose shader pass has LightMode "ZdAfterWash" (the rain of D-16) into the colour target AFTER the wash,
        /// depth-tested against the scene: the wash bleeds, posterises and inks, which eats thin strokes.
        /// </summary>
        private sealed class OverlayPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId Tag = new ShaderTagId("ZdAfterWash");

            private sealed class PassData { public RendererListHandle List; }

            public OverlayPass() { profilingSampler = new ProfilingSampler("Zelda After Wash"); }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var renderingData = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();

                var drawing = RenderingUtils.CreateDrawingSettings(Tag, renderingData, camera, lights, SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.transparent);
                var rlParams = new RendererListParams(renderingData.cullResults, drawing, filtering);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Zelda After Wash", out var data, profilingSampler))
                {
                    data.List = renderGraph.CreateRendererList(rlParams);
                    builder.UseRendererList(data.List);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.List));
                }
            }
        }

        private sealed class WatercolorPass : ScriptableRenderPass
        {
            private Material _material;
            private WatercolorSettings _s;
            private Texture2D _paper;

            private static readonly int LineColor = Shader.PropertyToID("_LineColor");
            private static readonly int LineParams = Shader.PropertyToID("_LineParams");
            private static readonly int WashParams = Shader.PropertyToID("_WashParams");
            private static readonly int PaperParams = Shader.PropertyToID("_PaperParams");
            private static readonly int PaperColor = Shader.PropertyToID("_PaperColor");
            private static readonly int GradeParams = Shader.PropertyToID("_GradeParams");
            private static readonly int WarmTint = Shader.PropertyToID("_WarmTint");
            private static readonly int VignetteColor = Shader.PropertyToID("_VignetteColor");
            private static readonly int ToneParams = Shader.PropertyToID("_ToneParams");
            private static readonly int NightTint = Shader.PropertyToID("_NightTint");
            private static readonly int NightPaper = Shader.PropertyToID("_NightPaper");
            private static readonly int NightVignette = Shader.PropertyToID("_NightVignette");
            private static readonly int PaperTex = Shader.PropertyToID("_PaperTex");

            private sealed class PassData
            {
                public TextureHandle Source;
                public Material Material;
            }

            public WatercolorPass() { profilingSampler = new ProfilingSampler("Zelda Watercolor"); }

            public void Setup(Material material, WatercolorSettings s, Texture2D paper)
            {
                _material = material;
                _s = s;
                _paper = paper;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return; // cannot sample the back buffer; requiresIntermediateTexture normally prevents this

                float heightScale = camera.cameraTargetDescriptor.height / Mathf.Max(1f, _s.lineReferenceHeight);
                _material.SetVector(LineColor, _s.lineColor);
                _material.SetVector(LineParams, new Vector4(Mathf.Max(1f, _s.lineWidthPx * heightScale), _s.depthThreshold, _s.normalThreshold, _s.lineWobble));
                _material.SetVector(WashParams, new Vector4(_s.toneLevels, _s.posterize, _s.bleedPx * heightScale, _s.edgeDarken));
                _material.SetVector(PaperParams, new Vector4(_s.paperTilePx * Mathf.Max(0.5f, heightScale), _s.grain, _s.blotch, _s.pigment));
                _material.SetColor(PaperColor, _s.paperColor);
                _material.SetVector(GradeParams, new Vector4(_s.saturation * StateSaturation, _s.contrast, _s.vignette, _s.vignetteStart));
                _material.SetColor(WarmTint, _s.warmTint);
                _material.SetColor(VignetteColor, _s.vignetteColor);
                _material.SetVector(ToneParams, new Vector4(_s.toneCurve, 0f, 0f, 0f));
                _material.SetColor(NightTint, _s.nightTint);
                _material.SetColor(NightPaper, _s.nightPaper);
                _material.SetColor(NightVignette, _s.nightVignette);
                _material.SetTexture(PaperTex, _paper);

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_ZeldaWatercolorTarget";
                desc.clearBuffer = false;
                desc.msaaSamples = MSAASamples.None;
                TextureHandle target = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Zelda Watercolor", out var data, profilingSampler))
                {
                    data.Source = source;
                    data.Material = _material;
                    builder.UseTexture(source, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
                }
                resources.cameraColor = target;
            }
        }
    }
}
