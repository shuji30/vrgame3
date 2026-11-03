using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// どのシーン(ハブ・各会場)にも置く共通の物を組み立てるエディタ処理。
    /// ・シーン切り替えのフェード(docs/art-direction.md 7.4)
    /// ・メニューボタンのメニューと確認(6.5)、共通の設定パネル(6.6)
    /// ・ランクのメダル(6.8)
    /// XR Origin を置いたあとに呼ぶ。
    /// </summary>
    public static class CommonSceneBuilder
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Common";
        private const string FadeShaderName = "VRGame3/FadeSphere";

        /// <summary>メインカメラの子にフェード用の球を付ける。失敗したときは理由を返す。</summary>
        public static string AddFader()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return "メインカメラが見つからないので、シーン切り替えのフェードを付けませんでした";
            }

            Shader shader = Shader.Find(FadeShaderName);
            if (shader == null)
            {
                return $"シェーダー {FadeShaderName} が見つからないので、フェードを付けませんでした";
            }

            SceneBuildUtility.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/C_Fade.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "フェード";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(camera.transform, false);
            // 半径 0.3m(カメラの手前の切り取り距離より外、手やコントローラーより内側)
            sphere.transform.localScale = Vector3.one * 0.6f;
            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            var fader = sphere.AddComponent<ScreenFader>();
            var audio = sphere.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var so = new SerializedObject(fader);
            so.FindProperty("sphere").objectReferenceValue = renderer;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("fadeOutClip").objectReferenceValue = PlaceholderClip("SFX_SceneTransition");
            so.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        /// <summary>
        /// メニューと設定パネルを作る。inHub が true なら「ハブへもどる」を置かない。失敗したときは理由を返す。
        /// </summary>
        public static string AddMenuAndSettings(Transform parent, bool inHub, out SettingsPanel settingsPanel)
        {
            settingsPanel = null;
            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                return $"日本語フォントが見つかりません({UIBuildUtility.FontPath})。メニューは作りませんでした";
            }

            UIBuildUtility.EnsureEventSystem();
            var root = new GameObject("メニューと設定");
            root.transform.SetParent(parent, false);

            settingsPanel = BuildSettingsPanel(root.transform, font);
            BuildMenu(root.transform, font, inHub, settingsPanel);
            BuildRestReminder(root.transform, font, inHub);
            return null;
        }

        /// <summary>休憩案内パネル(6.7)。ハブではいつでも区切りとみなす。</summary>
        private static void BuildRestReminder(Transform parent, TMP_FontAsset font, bool inHub)
        {
            var holder = new GameObject("休憩案内");
            holder.transform.SetParent(parent, false);
            var reminder = holder.AddComponent<RestReminder>();

            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "休憩案内のパネル", new Vector2(760f, 420f),
                UIBuildUtility.Background);
            canvas.transform.position = new Vector3(0f, 1.45f, 1.2f);
            Transform c = canvas.transform;
            TMP_Text message = UIBuildUtility.Label(c, "文1", "20分あそびました。", font, 44, new Vector2(0f, 120f),
                new Vector2(720f, 60f), UIBuildUtility.TextLight);
            UIBuildUtility.Label(c, "文2", "ひと休みしませんか?", font, 44, new Vector2(0f, 60f), new Vector2(720f, 60f),
                UIBuildUtility.TextLight);
            UIBuildUtility.NormalButton(c, "今後は出さない", "今後は出さない", font, 34, new Vector2(-180f, -110f),
                new Vector2(320f, 96f), reminder.NeverShow);
            UIBuildUtility.PrimaryButton(c, "とじる", "とじる", font, 44, new Vector2(180f, -110f), new Vector2(320f, 96f),
                reminder.Close);

            var audio = holder.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var so = new SerializedObject(reminder);
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("alwaysAtBreak").boolValue = inHub;
            so.FindProperty("source").objectReferenceValue = audio;
            so.FindProperty("noticeClip").objectReferenceValue = PlaceholderClip("SFX_BreakNotice");
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
        }

        private static void BuildMenu(Transform parent, TMP_FontAsset font, bool inHub, SettingsPanel settingsPanel)
        {
            // GameMenu はいつも有効な物に付け、パネル(Canvas)は子にして表示を切り替える
            var holder = new GameObject("メニュー");
            holder.transform.SetParent(parent, false);
            var menu = holder.AddComponent<GameMenu>();

            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "メニューのパネル", new Vector2(700f, 520f),
                Color.clear);
            canvas.transform.position = new Vector3(0f, 1.45f, 1f);

            // つづける / せってい / ハブへもどる(6.5。ハブでは「ハブへもどる」がないので低くする)
            Vector2 size = inHub ? new Vector2(600f, 400f) : new Vector2(600f, 520f);
            GameObject main = UIBuildUtility.Page(canvas.transform, "メニュー", size, UIBuildUtility.Background);
            Transform m = main.transform;
            float top = inHub ? 140f : 200f;
            UIBuildUtility.Label(m, "見出し", "メニュー", font, 52, new Vector2(0f, top), new Vector2(560f, 70f),
                UIBuildUtility.TextLight);
            UIBuildUtility.PrimaryButton(m, "つづける", "つづける", font, 44, new Vector2(0f, top - 120f),
                new Vector2(440f, 96f), menu.Close);
            UIBuildUtility.NormalButton(m, "せってい", "せってい", font, 44, new Vector2(0f, top - 240f),
                new Vector2(440f, 96f), menu.OpenSettings);
            GameObject hubButton = null;
            if (!inHub)
            {
                (Image hubImage, _) = UIBuildUtility.NormalButton(m, "ハブへもどる", "ハブへもどる", font, 44,
                    new Vector2(0f, top - 360f), new Vector2(440f, 96f), menu.RequestHub);
                hubButton = hubImage.gameObject;
            }

            // ゲームの途中で「ハブへもどる」を押したときの確認
            GameObject confirm = UIBuildUtility.Page(canvas.transform, "確認", new Vector2(700f, 420f), UIBuildUtility.Background);
            Transform cf = confirm.transform;
            UIBuildUtility.Label(cf, "文1", "ゲームのとちゅうです。", font, 40, new Vector2(0f, 110f), new Vector2(660f, 56f),
                UIBuildUtility.TextLight);
            UIBuildUtility.Label(cf, "文2", "ハブへもどりますか?", font, 40, new Vector2(0f, 60f), new Vector2(660f, 56f),
                UIBuildUtility.TextLight);
            UIBuildUtility.Label(cf, "補足", "スコアは記録されません", font, 30, new Vector2(0f, -10f), new Vector2(660f, 44f),
                UIBuildUtility.TextSub);
            UIBuildUtility.NormalButton(cf, "もどる", "もどる", font, 40, new Vector2(-160f, -130f), new Vector2(280f, 96f),
                menu.GoToHub);
            UIBuildUtility.PrimaryButton(cf, "つづける", "つづける", font, 40, new Vector2(160f, -130f), new Vector2(280f, 96f),
                menu.CancelHub);
            confirm.SetActive(false);

            var so = new SerializedObject(menu);
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("mainPage").objectReferenceValue = main;
            so.FindProperty("confirmPage").objectReferenceValue = confirm;
            so.FindProperty("hubButton").objectReferenceValue = hubButton;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
        }

        private static SettingsPanel BuildSettingsPanel(Transform parent, TMP_FontAsset font)
        {
            var holder = new GameObject("設定");
            holder.transform.SetParent(parent, false);
            var panel = holder.AddComponent<SettingsPanel>();

            // 6.6:身長・利き手・連続移動・移動の速さ・休憩案内
            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "設定のパネル", new Vector2(800f, 860f),
                UIBuildUtility.Background);
            canvas.transform.position = new Vector3(0f, 1.45f, 1f);
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;

            UIBuildUtility.Label(c, "見出し", "せってい", font, 52, new Vector2(0f, 370f), new Vector2(760f, 70f), light);

            UIBuildUtility.Label(c, "身長の見出し", "身長", font, 36, new Vector2(-270f, 250f), new Vector2(240f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を下げる", "-", font, 48, new Vector2(-80f, 250f), new Vector2(100f, 76f),
                panel.DecreaseHeight);
            TMP_Text heightText = UIBuildUtility.Label(c, "身長の数字", "170 cm", font, 40, new Vector2(80f, 250f),
                new Vector2(200f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を上げる", "+", font, 48, new Vector2(240f, 250f), new Vector2(100f, 76f),
                panel.IncreaseHeight);

            var images = new Image[9];
            var texts = new TMP_Text[9];
            var buttons = new Button[9];

            void Choice(SettingsPanel.Choice choice, string label, float x, float y, Vector2 size, float fontSize,
                UnityEngine.Events.UnityAction action)
            {
                (Image image, TMP_Text text) = UIBuildUtility.NormalButton(c, choice.ToString(), label, font, fontSize,
                    new Vector2(x, y), size, action);
                images[(int)choice] = image;
                texts[(int)choice] = text;
                buttons[(int)choice] = image.GetComponent<Button>();
            }

            Vector2 pair = new Vector2(170f, 76f);
            UIBuildUtility.Label(c, "利き手の見出し", "利き手", font, 36, new Vector2(-270f, 140f), new Vector2(240f, 60f), light);
            Choice(SettingsPanel.Choice.LeftHand, "左", -40f, 140f, pair, 38, panel.SelectLeftHand);
            Choice(SettingsPanel.Choice.RightHand, "右", 160f, 140f, pair, 38, panel.SelectRightHand);

            UIBuildUtility.Label(c, "連続移動の見出し", "連続移動", font, 36, new Vector2(-270f, 30f), new Vector2(240f, 60f), light);
            Choice(SettingsPanel.Choice.MoveOff, "オフ", -40f, 30f, pair, 38, panel.MoveOff);
            Choice(SettingsPanel.Choice.MoveOn, "オン", 160f, 30f, pair, 38, panel.MoveOn);

            UIBuildUtility.Label(c, "速さの見出し", "移動の速さ", font, 34, new Vector2(-270f, -80f), new Vector2(240f, 60f), light);
            Vector2 speed = new Vector2(140f, 76f);
            Choice(SettingsPanel.Choice.SpeedSlow, "おそい", -60f, -80f, speed, 34, panel.SpeedSlow);
            Choice(SettingsPanel.Choice.SpeedNormal, "ふつう", 90f, -80f, speed, 34, panel.SpeedNormal);
            Choice(SettingsPanel.Choice.SpeedFast, "はやい", 240f, -80f, speed, 34, panel.SpeedFast);

            UIBuildUtility.Label(c, "休憩案内の見出し", "休憩案内", font, 36, new Vector2(-270f, -190f), new Vector2(240f, 60f), light);
            Choice(SettingsPanel.Choice.RestOn, "オン", -40f, -190f, pair, 38, panel.RestOn);
            Choice(SettingsPanel.Choice.RestOff, "オフ", 160f, -190f, pair, 38, panel.RestOff);

            UIBuildUtility.PrimaryButton(c, "とじる", "とじる", font, 46, new Vector2(0f, -340f), new Vector2(340f, 96f),
                panel.Close);

            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("heightText").objectReferenceValue = heightText;
            SetArray(so.FindProperty("choiceImages"), images);
            SetArray(so.FindProperty("choiceTexts"), texts);
            SetArray(so.FindProperty("choiceButtons"), buttons);
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            return panel;
        }

        /// <summary>
        /// ランクのメダル(6.8)。diameter はメダルの直径(px)。名前の文字はメダルの右に置く。
        /// </summary>
        public static RankMedal AddRankMedal(Transform parent, TMP_FontAsset font, Vector2 position, float diameter,
            float nameFontSize)
        {
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var holder = new GameObject("ランク", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            ((RectTransform)holder.transform).anchoredPosition = position;

            float rimSize = diameter + diameter * 0.1f;
            Image rim = Circle(holder.transform, "縁", circle, rimSize, Color.white);

            // にじいろの帯:4つの扇形を 90° ずつ
            var rainbow = new GameObject("にじいろの帯", typeof(RectTransform));
            rainbow.transform.SetParent(holder.transform, false);
            string[] bandColors = { "#FF7F6B", "#FFD24A", "#6CC36A", "#3AA7FF" };
            for (int i = 0; i < 4; i++)
            {
                Image band = Circle(rainbow.transform, $"帯{i + 1}", circle, rimSize + diameter * 0.04f, Hex(bandColors[i]));
                band.type = Image.Type.Filled;
                band.fillMethod = Image.FillMethod.Radial360;
                band.fillAmount = 0.25f;
                band.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
            }

            rainbow.SetActive(false);

            Image face = Circle(holder.transform, "メダル", circle, diameter, Color.white);

            Image pearlImage = Circle(holder.transform, "しんじゅ", circle, diameter * 0.22f, Hex("#FFFDF7"));
            ((RectTransform)pearlImage.transform).anchoredPosition = new Vector2(0f, diameter * 0.62f);
            pearlImage.gameObject.SetActive(false);

            TMP_Text stars = UIBuildUtility.Label(holder.transform, "星", "★", font, diameter * 0.28f, Vector2.zero,
                new Vector2(diameter, diameter * 0.5f), UIBuildUtility.TextDark);
            TMP_Text rankName = UIBuildUtility.Label(holder.transform, "名前", "かいがら", font, nameFontSize,
                new Vector2(diameter * 0.6f + nameFontSize * 2.2f, 0f), new Vector2(nameFontSize * 4.4f, nameFontSize * 1.4f),
                UIBuildUtility.TextLight);
            rankName.alignment = TextAlignmentOptions.Left;

            var medal = holder.AddComponent<RankMedal>();
            var so = new SerializedObject(medal);
            so.FindProperty("face").objectReferenceValue = face;
            so.FindProperty("rim").objectReferenceValue = rim;
            so.FindProperty("stars").objectReferenceValue = stars;
            so.FindProperty("rankName").objectReferenceValue = rankName;
            so.FindProperty("pearl").objectReferenceValue = pearlImage.gameObject;
            so.FindProperty("rainbow").objectReferenceValue = rainbow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return medal;
        }

        /// <summary>
        /// 結果の表示(6.9)。position はパネルの中心、eye はプレイヤーの目(パネルをそちらへ向ける)。
        /// 失敗したら null。
        /// </summary>
        public static ResultPanel AddResultPanel(Transform parent, Vector3 position, Vector3 eye)
        {
            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                return null;
            }

            UIBuildUtility.EnsureEventSystem();
            var holder = new GameObject("結果");
            holder.transform.SetParent(parent, false);
            var panel = holder.AddComponent<ResultPanel>();

            var root = new GameObject("結果のパネル");
            root.transform.SetParent(holder.transform, false);
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye, Vector3.up));

            // 看板らしく見せる木の枠(パネルの後ろ)
            GameObject frame = SceneBuildUtility.CreatePrimitive("枠", PrimitiveType.Cube, Vector3.zero,
                new Vector3(1.7f, 1.2f, 0.06f), SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "C_Wood", "#C98B55", false),
                false);
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0f, 0.035f);
            frame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            GameObject canvas = UIBuildUtility.CreateCanvas(root.transform, "文字", new Vector2(1600f, 1100f),
                UIBuildUtility.Background);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;

            TMP_Text headline = UIBuildUtility.Label(c, "見出し", "けっか", font, 110, new Vector2(0f, 440f), new Vector2(1500f, 140f), light);
            TMP_Text total = UIBuildUtility.Label(c, "ごうけい", "ごうけい 0", font, 160, new Vector2(-150f, 280f),
                new Vector2(1100f, 190f), light);
            (Image badge, _) = UIBuildUtility.Button(c, "ベスト更新", "ベスト更新!", font, 70, new Vector2(550f, 280f),
                new Vector2(360f, 100f), UIBuildUtility.PrimaryColor, UIBuildUtility.TextDark, null);
            Object.DestroyImmediate(badge.GetComponent<Button>());
            TMP_Text best = UIBuildUtility.Label(c, "ベスト", "ベスト 0", font, 80, new Vector2(0f, 140f), new Vector2(1500f, 110f),
                light);
            RankMedal medal = AddRankMedal(c, font, new Vector2(-560f, -20f), 180f, 90f);
            TMP_Text points = UIBuildUtility.Label(c, "ポイント", "ポイント 0(+0)", font, 80, new Vector2(330f, -20f),
                new Vector2(800f, 110f), light);
            TMP_Text rankUp = UIBuildUtility.Label(c, "ランクアップ", "ランクアップ!", font, 90, new Vector2(0f, -170f),
                new Vector2(1500f, 120f), UIBuildUtility.PrimaryColor);
            UIBuildUtility.PrimaryButton(c, "もういちど", "もういちど", font, 80, new Vector2(-300f, -400f),
                new Vector2(520f, 150f), panel.OnRetry);
            UIBuildUtility.NormalButton(c, "ハブへ", "ハブへ", font, 80, new Vector2(300f, -400f), new Vector2(520f, 150f),
                panel.OnHub);

            // パネルが隠れている間(休憩案内やメニュー)でも鳴らせるよう、いつも有効な物に付ける
            var audio = holder.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = root;
            so.FindProperty("totalText").objectReferenceValue = total;
            so.FindProperty("headlineText").objectReferenceValue = headline;
            so.FindProperty("newBestBadge").objectReferenceValue = badge.gameObject;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("medal").objectReferenceValue = medal;
            so.FindProperty("pointsText").objectReferenceValue = points;
            so.FindProperty("rankUpText").objectReferenceValue = rankUp.gameObject;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("resultClip").objectReferenceValue = PlaceholderClip("SFX_Result");
            so.FindProperty("rankUpClip").objectReferenceValue = PlaceholderClip("SFX_RankUp");
            so.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return panel;
        }

        /// <summary>
        /// 汎用のパネル(見出し・本文・ボタン4つ。1000 × 760px)。交代の案内・順位表・チャレンジに使う。
        /// position はパネルの中心、eye はプレイヤーの目。失敗したら null。
        /// </summary>
        public static ChoicePanel AddChoicePanel(Transform parent, Vector3 position, Vector3 eye)
        {
            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                return null;
            }

            UIBuildUtility.EnsureEventSystem();
            var holder = new GameObject("案内とえらぶパネル");
            holder.transform.SetParent(parent, false);
            var panel = holder.AddComponent<ChoicePanel>();

            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "パネル", new Vector2(1000f, 760f), UIBuildUtility.Background);
            canvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye, Vector3.up));
            Transform c = canvas.transform;
            TMP_Text title = UIBuildUtility.Label(c, "見出し", "見出し", font, 52, new Vector2(0f, 320f), new Vector2(960f, 70f),
                UIBuildUtility.TextLight);
            TMP_Text body = UIBuildUtility.Label(c, "本文", "", font, 36, new Vector2(0f, 120f), new Vector2(920f, 300f),
                UIBuildUtility.TextLight);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.alignment = TextAlignmentOptions.Top;

            var images = new Image[4];
            var texts = new TMP_Text[4];
            UnityEngine.Events.UnityAction[] presses = { panel.OnButton0, panel.OnButton1, panel.OnButton2, panel.OnButton3 };
            for (int i = 0; i < 4; i++)
            {
                // 2列 × 2段(上の段が先)
                float x = i % 2 == 0 ? -235f : 235f;
                float y = i < 2 ? -150f : -280f;
                (Image image, TMP_Text text) = UIBuildUtility.NormalButton(c, $"ボタン{i + 1}", "", font, 36, new Vector2(x, y),
                    new Vector2(440f, 100f), presses[i]);
                images[i] = image;
                texts[i] = text;
            }

            var audio = holder.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("source").objectReferenceValue = audio;
            so.FindProperty("turnChangeClip").objectReferenceValue = PlaceholderClip("SFX_TurnChange");
            so.FindProperty("clearClip").objectReferenceValue = PlaceholderClip("SFX_ChallengeClear");
            so.FindProperty("failClip").objectReferenceValue = PlaceholderClip("SFX_ChallengeFail");
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            SetArray(so.FindProperty("buttonImages"), images);
            SetArray(so.FindProperty("buttonTexts"), texts);
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>
        /// 「はじめに」パネルにモードの行(Y modeY)と強さ / 人数の行(Y optionY)を足し、ModeSelector でつなぐ(6.10)。
        /// モードが4つなら幅 170、3つ以下なら 220 のボタンにする。startLabel と title は文字を差し替えるために渡す。
        /// </summary>
        public static ModeSelector AddModeRows(GameObject host, Transform canvas, TMP_FontAsset font, string sportId, SportMode[] modes,
            float modeY, float optionY, TMP_Text startLabel, TMP_Text title)
        {
            var selector = host.AddComponent<ModeSelector>();
            Color light = UIBuildUtility.TextLight;
            UIBuildUtility.Label(canvas, "モードの見出し", "モード", font, 36, new Vector2(-360f, modeY), new Vector2(260f, 60f), light);
            UIBuildUtility.Label(canvas, "つよさの見出し", "つよさ / にんずう", font, 26, new Vector2(-360f, optionY), new Vector2(260f, 60f), light);

            UnityEngine.Events.UnityAction[] modeActions =
                { selector.SelectMode0, selector.SelectMode1, selector.SelectMode2, selector.SelectMode3 };
            bool four = modes.Length >= 4;
            float[] xs = four ? new[] { -145f, 38f, 221f, 404f } : new[] { -110f, 125f, 360f };
            Vector2 size = four ? new Vector2(170f, 76f) : new Vector2(220f, 76f);
            var modeImages = new Image[modes.Length];
            var modeTexts = new TMP_Text[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                (modeImages[i], modeTexts[i]) = UIBuildUtility.NormalButton(canvas, $"モード{i + 1}", modes[i].ToString(), font,
                    four ? 30 : 32, new Vector2(xs[i], modeY), size, modeActions[i]);
            }

            UnityEngine.Events.UnityAction[] optionActions = { selector.SelectOption0, selector.SelectOption1, selector.SelectOption2 };
            float[] optionXs = { -110f, 125f, 360f };
            var optionImages = new Image[3];
            var optionTexts = new TMP_Text[3];
            var optionButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                (optionImages[i], optionTexts[i]) = UIBuildUtility.NormalButton(canvas, $"つよさ{i + 1}", "", font, 28,
                    new Vector2(optionXs[i], optionY), new Vector2(220f, 76f), optionActions[i]);
                optionButtons[i] = optionImages[i].GetComponent<Button>();
            }

            var so = new SerializedObject(selector);
            so.FindProperty("sportId").stringValue = sportId;
            SerializedProperty modeList = so.FindProperty("modes");
            modeList.arraySize = modes.Length;
            for (int i = 0; i < modes.Length; i++)
            {
                modeList.GetArrayElementAtIndex(i).enumValueIndex = (int)modes[i];
            }

            SetArray(so.FindProperty("modeImages"), modeImages);
            SetArray(so.FindProperty("modeTexts"), modeTexts);
            SetArray(so.FindProperty("optionImages"), optionImages);
            SetArray(so.FindProperty("optionTexts"), optionTexts);
            SetArray(so.FindProperty("optionButtons"), optionButtons);
            so.FindProperty("startLabel").objectReferenceValue = startLabel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();
            return selector;
        }

        /// <summary>
        /// 腕前ポイントの決まりのアセット(Resources/SkillPointRules.asset)を、無ければ既定値で作る。
        /// Inspector で基準スコアなどを調整できるようにするため。
        /// </summary>
        public static void EnsureSkillPointRules()
        {
            const string folder = "Assets/_Project/Common/Resources";
            const string path = folder + "/SkillPointRules.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SkillPointRules>(path);
            if (existing != null)
            {
                // 種目が増えたときは、足りない行を既定値で足す(調整した値は変えない)
                var fresh = ScriptableObject.CreateInstance<SkillPointRules>();
                var target = new SerializedObject(existing);
                var source = new SerializedObject(fresh);
                SerializedProperty have = target.FindProperty("sports");
                SerializedProperty all = source.FindProperty("sports");
                for (int i = 0; i < all.arraySize; i++)
                {
                    string id = all.GetArrayElementAtIndex(i).FindPropertyRelative("sportId").stringValue;
                    bool found = false;
                    for (int j = 0; j < have.arraySize; j++)
                    {
                        found |= have.GetArrayElementAtIndex(j).FindPropertyRelative("sportId").stringValue == id;
                    }

                    if (!found)
                    {
                        have.arraySize++;
                        SerializedProperty added = have.GetArrayElementAtIndex(have.arraySize - 1);
                        SerializedProperty from = all.GetArrayElementAtIndex(i);
                        added.FindPropertyRelative("sportId").stringValue = id;
                        added.FindPropertyRelative("divisor").floatValue = from.FindPropertyRelative("divisor").floatValue;
                        SerializedProperty scores = from.FindPropertyRelative("baseScores");
                        SerializedProperty to = added.FindPropertyRelative("baseScores");
                        to.arraySize = scores.arraySize;
                        for (int k = 0; k < scores.arraySize; k++)
                        {
                            to.GetArrayElementAtIndex(k).intValue = scores.GetArrayElementAtIndex(k).intValue;
                        }
                    }
                }

                target.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(fresh);
                return;
            }

            SceneBuildUtility.EnsureFolder(folder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SkillPointRules>(), path);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Art/Audio/Placeholder の仮の音。無ければ null。</summary>
        public static AudioClip PlaceholderClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Art/Audio/Placeholder/{name}.wav");
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static Image Circle(Transform parent, string name, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
