| [English](README.md) | 日本語 |

# Editor Patcher
Unity Editor を使いやすくするツール群です。

## インストール
1. [VPM Listing](https://vpm.nekobako.net) の `Add to VCC` ボタンを押してリポジトリを追加します。
2. プロジェクトの `Manage Project` ボタンを押します。
3. `Editor Patcher` パッケージの右にある `+` ボタンを押します。

## Usage

### Skinned Mesh Renderer Editor
- `Tools > Editor Patcher > Skinned Mesh Renderer Editor`
  - Skinned Mesh Renderer の Inspector のブレンドシェイプ一覧を検索と絞り込みに対応したものに置き換えます。
  - Skinned Mesh Renderer の Inspector にボーン一覧を追加します。

![Skinned Mesh Renderer Editor](https://github.com/user-attachments/assets/1bff1f3b-907a-4f5a-b042-364a72990d63)

### Avatar Preview
- `Tools > Editor Patcher > Avatar Preview`
  - Avatar Preview のカメラの視点を揃えたり、毎回リセットされないように固定します。

https://github.com/user-attachments/assets/b3c86ca5-17aa-4cb6-a69d-200744819162

### Object Shelf
- `Tools > Editor Patcher > Object Shelf > Auto Spawn`
  - オブジェクトの参照を保持できるウィンドウを D&D 時に自動で表示します。
- `Tools > Editor Patcher > Object Shelf > Manual Spawn`
  - オブジェクトの参照を保持できるウィンドウを手動で表示します。

https://github.com/user-attachments/assets/d86f9f7d-83e6-4ec2-b1dd-cfe2e1a27276

### Platform Switcher
- `Tools > Editor Patcher > Platform Switcher`
  - メインツールバーにプラットフォームを切り替えるボタンを追加します。

![Platform Switcher](https://github.com/user-attachments/assets/7864e699-26e3-42f7-af0c-5109b08fe269)

### Rebase Prefab
- `Assets > Prefab > Rebase and Keep All...`
  - 全てのプロパティを維持した状態で、指定したプレハブを親とするプレハブバリアントを再帰的に生成します。
  - プレハブの右クリックメニューから実行できます。
- `Assets > Prefab > Rebase and Keep Overrides...`
  - 全てのプレハブオーバーライドを維持した状態で、指定したプレハブを親とするプレハブバリアントを再帰的に生成します。
  - プレハブの右クリックメニューから実行できます。

### Fix Prefab Override
- `Tools > Editor Patcher > Fix Prefab Override > Enable Auto Fix`
  - オーバーライド前と同じ値のプレハブオーバーライドを自動でリバートします。
  - Unity 2021.3 以降のみ実行できます。
- `Tools > Editor Patcher > Fix Prefab Override > Fix All in Project`
  - プロジェクト内の全てのプレハブにおいて、オーバーライド前と同じ値のプレハブオーバーライドをリバートします。
  - 実行前にプロジェクトのバックアップを取ってください。
- `GameObject > Editor Patcher > Fix Prefab Override`
  - 選択したプレハブにおいて、オーバーライド前と同じ値のプレハブオーバーライドをリバートします。
  - プレハブの右クリックメニューから実行できます。
