using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// 演出のパーティクルを作るエディタ処理の共通部品(docs/art-direction.md 3.5)。
    /// Unlit・不透明(半透明にしない)、1回だけ出す(Burst)。
    /// </summary>
    public static class ParticleBuildUtility
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Common";

        /// <summary>パーティクル用の不透明な Unlit マテリアル(頂点色で色を付ける)。</summary>
        public static Material GetOrCreateMaterial()
        {
            SceneBuildUtility.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/C_Particle.mat";
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

        /// <summary>
        /// 1回だけ出すパーティクル。colors からランダムに1色。shrink が true なら縮みながら消える。
        /// </summary>
        public static ParticleSystem Burst(Transform parent, string name, Vector3 position, int count, string[] colors,
            Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, float radius, bool upward, bool shrink, bool spin)
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
            main.duration = lifetime.y;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.startColor = RandomColors(colors);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = system.shape;
            if (upward)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 35f;
                shape.radius = radius;
                shape.rotation = new Vector3(-90f, 0f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
            }

            if (shrink)
            {
                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }

            if (spin)
            {
                ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        /// <summary>指定した色からランダムに1色選ぶ(色が混ざらないよう、段階的なグラデーションにする)。</summary>
        public static ParticleSystem.MinMaxGradient RandomColors(string[] hexColors)
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
    }
}
