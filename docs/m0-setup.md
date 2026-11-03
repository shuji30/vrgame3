# M0 の作業手順(Unity エディタでの作業)

M0 のゲート(`docs/plan.md` の「M0 準備」)を実機で確かめるまでの手順をまとめる。
Claude Code が書いたスクリプトは未確認なので、エラーや違和感があれば Console の文面か感想をそのまま伝える。

## 用意したもの

| ファイル | 役割 |
| --- | --- |
| `Assets/_Project/Common/Scripts/Locomotion/TrackpadLocomotion.cs` | トラックパッドの振り分け。中央の押し込みでテレポート(押している間は放物線、離すと移動)、左右の押し込みでスナップターン |
| `Assets/_Project/Common/Scripts/Interaction/ThrowableObject.cs` | 掴んで投げる共通部品。離す直前の動きから速度を求め、倍率をかけて投げる。手を離れている間は連続衝突判定にしてすり抜けを防ぐ |
| `Assets/_Project/Common/Scripts/Testing/ThrowAccuracyTest.cs` | ゲート確認用。10回投げて、目標の左右1m以内に落ちた回数を数える |
| `Assets/_Project/Common/Editor/M0SandboxBuilder.cs` | メニュー「VRGame3 > M0 テスト場を作る」で、確認用のシーンを組み立てる |

テンプレートのサンプル(`SampleScene`、`TutorialInfo`、`Readme.asset`)は削除した。

## 1. プロジェクトを開いてコンパイルを確かめる

- [ ] Unity Hub からプロジェクトを開く(git pull 後)
- [ ] Console にエラー(赤)が出ていないか見る。出ていたら文面をそのまま Claude Code に渡す

## 2. OpenXR の設定

- [ ] Edit > Project Settings > XR Plug-in Management を開き、PC(Windows)タブで **OpenXR** にチェックが入っているか確認
- [ ] 同じ画面の左の一覧から **OpenXR** を開き、PC タブの **Enabled Interaction Profiles** の「+」から **HTC Vive Controller Profile** を追加する
- [ ] 同じ画面の **Render Mode** が **Single Pass Instanced** になっているか確認
- [ ] **Play Mode OpenXR Runtime** を **SteamVR** にする
- [ ] 警告の三角マーク(Project Validation)が出ていたら、内容を見て「Fix」で直してよいか判断する(迷ったら文面を Claude Code に渡す)

## 3. 描画設定(docs/art-direction.md の 3.6)

- [ ] `Assets/Settings/PC_RPAsset` の Depth Texture と Opaque Texture をオフ、MSAA を 4x にする
- [ ] `Assets/Settings/SampleSceneProfile` から Motion Blur と Vignette を外す
- その他の項目は art-direction.md の 3.6 に従う

## 4. テスト場を作る

- [ ] メニュー **VRGame3 > M0 テスト場を作る** を押す
- [ ] `Assets/_Project/Common/Scenes/M0_Sandbox.unity` ができ、開いた状態になる
- [ ] 終わりのダイアログに「手で確認が必要な点」が出たら、下の「自動設定が失敗したとき」を行う

テスト場の中身:

- 床(40m 四方)、奥の壁(14m 先。すり抜け確認用)、ボール置き台、ボール3個
- 目標:8m 先の黄色い円盤。直径2m なので、円盤に落ちれば左右1m以内の目安になる
- XR Origin(XRI の Starter Assets の物)。次の変更を自動で行う
  - サンプルの移動方式(Move / Turn / Teleportation / Grab Move / Climb / Gravity と Teleport Interactor)を無効にする
  - 掴む操作をグリップからトリガーに差し替える
  - 左右のコントローラーに TrackpadLocomotion と、放物線・着地点の表示を付ける

### 自動設定が失敗したとき

- 左右の **Near-Far Interactor**(XR Origin > Camera Offset > Left/Right Controller の下)を選び、**Select Input** の
  - Input Action Performed → `XRI Left Interaction/Activate`(右手は Right)
  - Input Action Value → `XRI Left Interaction/Activate Value`(右手は Right)
- Left/Right Controller に **Trackpad Locomotion** を追加し、Hand を左右に合わせ、Xr Origin に XR Origin を入れる

## 5. 実機で確かめる(M0 のゲート)

SteamVR を起動し、ヘッドセットとコントローラーをつないでから Play を押す。

- [ ] **掴んで投げる**:台のボールにコントローラーを近づけ、トリガーを引いて掴み、目標に向けて投げて離す
- [ ] **命中の確認**:10回投げる。落ちた地点に青(命中)か赤(外れ)の玉が置かれ、10回目で目標の円盤が青(合格:8回以上)か赤(不合格)に変わる。Console にも毎回の結果が出る
  - 2m より手前に落ちたもの(取り落とし)は数えない
  - 続けて投げると次のセットになる。途中で数え直すときは、目標の ThrowAccuracyTest の右上メニュー(⋮)から「数え直す」
  - ボールが足りなくなったら、テレポートで拾いに行くか Play をやり直す
- [ ] **すり抜けない**:床に強く叩きつける、奥の壁に思い切り投げる。床や壁を抜けて落ちないこと
- [ ] **テレポート**:トラックパッドの中央を押し込む → 青い放物線と着地点が出る → 離すとその場所へ移動する。壁など移動できない所は赤い線になり、離しても動かない
- [ ] **スナップターン**:トラックパッドの左右を押し込むと、1回につき45度回る。テレポートと取り違えないこと
- [ ] **頭と手の追従**:コントローラーの模型(サンプルの仮モデル)が手の動きに遅れずついてくる
- [ ] **fps**:SteamVR の「フレームタイミングを表示」で、上の操作中に 60fps(16.7ms)を保っているか見る

## 6. 感想を伝えて調整する

数値は Inspector から変えられる。変えてみて良かった値を Claude Code に伝えれば既定値に反映する。

| 感想の例 | 触る場所 | 項目 |
| --- | --- | --- |
| ボールが飛ばない/飛びすぎる | ボールの Throwable Object | Velocity Scale(投げる力の倍率) |
| 投げる向きがぶれる | ボールの Throwable Object | Sample Window(長くすると安定) |
| テレポートとスナップターンを取り違える | コントローラーの Trackpad Locomotion | Center Threshold |
| 回る角度が大きい/小さい | コントローラーの Trackpad Locomotion | Snap Turn Angle |
| テレポートの届く距離 | コントローラーの Trackpad Locomotion | Arc Launch Speed |
| ボールが重い/軽い | ボールの Rigidbody | Mass(いまは 2kg) |

## まだ入れていないもの(M0 の残り)

- 掴む・離すの振動の調整(art-direction.md の M0 の項)
- 共有パレット v0 と `M_Palette`(アート側の M0 の作業)。テスト場の色は仮のマテリアル
