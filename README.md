# vrgame3

個人制作のVRゲームプロジェクトです。

## 開発環境

- ゲームエンジン: Unity 6(6000.6.5f1)、描画は URP
- 対象デバイス: Pimax 8KX / Pimax シリーズ(SteamVR 経由)、HTC Vive コントローラー
- XR: OpenXR(SteamVR ランタイム)/ XR Interaction Toolkit 3.6.1

## セットアップ

1. このリポジトリをクローン
   ```
   git clone https://github.com/shuji30/vrgame3.git
   ```
2. Unity Hub の「追加」からこのフォルダを開く

企画書は `docs/plan.md` を参照。

## メモ

- `Library/` や `Temp/` などの自動生成フォルダは `.gitignore` で除外しています。
- テクスチャやモデルなど大きなファイルが増えてきたら Git LFS の導入を検討してください。
