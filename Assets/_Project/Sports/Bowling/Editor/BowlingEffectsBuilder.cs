using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// ボウリング場の演出を組み込むエディタ処理。紙吹雪(ストライク・スペア)とガターの煙のパーティクルを作り、
    /// ピンのマテリアルの発光を有効にして、BowlingEffects でゲームにつなぐ。docs/art-direction.md 3.5・7.2。
    /// パーティクルは Unlit・不透明(半透明にしない)・同時 300 個以下。
    /// </summary>
    internal static class BowlingEffectsBuilder
    {
        public static void AddEffects(GameObject root, BowlingGame game, PinSetter pinSetter, Material pinMaterial,
            string materialFolder, float laneX, float pinDeckZ)
        {
            EnableGlow(pinMaterial);
            Material particle = GetOrCreateParticleMaterial(materialFolder);

            var effects = new GameObject("演出");
            effects.transform.SetParent(root.transform, false);

            // 紙吹雪はピン置き場の上(プレイヤーから約 20m)から降らせる
            Vector3 confettiPosition = new Vector3(laneX, 2.6f, pinDeckZ + 0.5f);
            ParticleSystem strike = Confetti(effects.transform, "ストライクの紙吹雪", confettiPosition, 200, particle,
                new[] { "#FFD24A", "#FF7F6B", "#3AA7FF", "#6CC36A", "#FFFDF7" });
            ParticleSystem spare = Confetti(effects.transform, "スペアの紙吹雪", confettiPosition, 70, particle,
                new[] { "#3AA7FF", "#7FE3D6", "#FFFDF7" });
            ParticleSystem gutter = Puff(effects.transform, "ガターの煙", new Vector3(laneX, -0.3f, pinDeckZ + 1.5f), particle);

            var component = root.AddComponent<BowlingEffects>();
            var so = new SerializedObject(component);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("pinSetter").objectReferenceValue = pinSetter;
            so.FindProperty("strikeConfetti").objectReferenceValue = strike;
            so.FindProperty("spareConfetti").objectReferenceValue = spare;
            so.FindProperty("gutterPuff").objectReferenceValue = gutter;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>ピンのマテリアルの発光を有効にしておく(普段は黒=光らない。演出のときだけ色を入れる)。</summary>
        private static void EnableGlow(Material material)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
        }

        private static ParticleSystem Confetti(Transform parent, string name, Vector3 position, int count, Material material,
            string[] colors)
        {
            ParticleSystem system = CreateSystem(parent, name, position, material);

            ParticleSystem.MainModule main = system.main;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            main.maxParticles = count;
            main.startColor = RandomColors(colors);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // ひらひら舞うように回転させる
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            return system;
        }

        private static ParticleSystem Puff(Transform parent, string name, Vector3 position, Material material)
        {
            ParticleSystem system = CreateSystem(parent, name, position, material);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 20;
            main.startColor = RandomColors(new[] { "#B8B4C8", "#FFFDF7" });

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            // 小さく縮みながら消える(半透明にしないため、大きさで消す)
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            return system;
        }

        private static ParticleSystem CreateSystem(Transform parent, string name, Vector3 position, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        /// <summary>指定した色からランダムに1色選ぶ(色が混ざらないよう、段階的なグラデーションにする)。</summary>
        private static ParticleSystem.MinMaxGradient RandomColors(string[] hexColors)
        {
            var keys = new GradientColorKey[hexColors.Length];
            for (int i = 0; i < hexColors.Length; i++)
            {
                ColorUtility.TryParseHtmlString(hexColors[i], out Color color);
                keys[i] = new GradientColorKey(color, (i + 1f) / hexColors.Length);
            }

            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        /// <summary>パーティクル用の不透明な Unlit マテリアル(頂点色で色を付ける)。</summary>
        private static Material GetOrCreateParticleMaterial(string folder)
        {
            SceneBuildUtility.EnsureFolder(folder);
            string path = $"{folder}/B_Particle.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
