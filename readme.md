# RoomVisibilityManager

VRChat + UdonSharp 向けの部屋単位表示制御コンポーネントです。  
プレーヤーが今どの部屋にいるかを `BoxCollider` で判定し、`室外` と `各部屋` の表示状態を表形式で切り替えます。

この README は、利用者向け手順だけでなく、次回以降の保守で必要になる実装前提も残すことを目的にしています。

## 1. 何をするコンポーネントか

- 各 `roomRoot` を 1 部屋として扱う
- Editor の `セットアップ実行` で各部屋の Bounds から判定用バウンディングボックスを自動生成する
- プレーヤー位置に応じて、`nonRoomRoots` 配下と `roomRoots` 配下の表示を切り替える
- `Renderer.enabled` / `Canvas.enabled` / `Light.enabled` と `GameObject.SetActive` の 4 系統で切り替えられる
- 必要なら `Terrain` も GameObject 単位で切り替えられる

想定用途は「オクルージョンカリングの代わりに、部屋ごとに遠景や別エリアを単純に隠したい」ケースです。

## 2. ファイル構成

- `RoomVisibilityManager.cs`
  - Runtime 本体。部屋判定と表示切替を担当
- `RoomVisibilityBoundingBox.cs`
  - 自動生成されたバウンディングボックスに付く補助コンポーネント。Gizmo 表示を担当
- `Editor/RoomVisibilityManagerEditor.cs`
  - 専用 Inspector。設定 UI、バウンディングボックス生成、キャッシュ再構築、検証を担当
- `RoomVisibilityManager.asset`
  - アセット本体

## 3. 実装の全体像

処理は大きく 2 段階です。

### 3.1 Editor 側

`セットアップ実行` で次をまとめて行います。

1. 入力値の最低限チェック
2. `roomRoots` から有効な部屋一覧を作成
3. 各部屋の Renderer / Collider から Bounds を集計
4. Bounds から `BoxCollider` の部屋バウンディングボックスを生成
5. 表示切替対象 Renderer / Canvas / Light / Terrain / GameObject を収集
6. 結果を `controlledRenderers` などの配列へ保存
7. 同一 `RoomVisibilityManager` 内の部屋重なりと、同一シーン内の別 `RoomVisibilityManager` との重なりを検出して警告する
8. 警告や統計を `lastBuildReport` へ保存

Runtime はこの生成済み配列をそのまま使います。  
つまり、`roomRoots` や `nonRoomRoots` を変えたあとに `セットアップ実行` しないと、Runtime の実際の制御対象は更新されません。

### 3.2 Runtime 側

`Update()` で一定間隔ごとに現在部屋を判定し、部屋が変わったときだけ表示を更新します。

- 判定点は `AvatarRoot` または `Head`
- 判定開始は `startupDelaySeconds` 後
- 判定周期は `checkInterval`
- 部屋判定は `roomBoundingBoxes` の `BoxCollider` に対して手動の AABB 判定で行う

現在位置に応じて、表示制御表 `roomHiddenMatrix` の

- 行: 現在位置 (`室外` + 各部屋)
- 列: 表示対象 (`室外` + 各部屋)

を参照して ON/OFF を決めます。対角セルは常に `true` です。

## 4. 主要なデータの意味

### 4.1 ユーザー入力

- `roomRoots`
  - 各要素の配下全体を 1 部屋として扱う
- `nonRoomRoots`
  - 部屋に入ったとき隠したい、または室外設定に従って表示したい対象の親
- `visibilityExcludeRoots`
  - 表示制御対象から完全に除外する親
- `activeToggleRoots`
  - `Renderer.enabled` ではなく `GameObject.SetActive` で切り替える親
- `roomHiddenMatrix`
  - 表示制御表
- `boundsIgnoreRoots`
  - 部屋 Bounds 計算から除外する親

### 4.2 Editor が生成するキャッシュ

- `roomBoundingBoxes`
  - 各部屋の判定用 `BoxCollider`
- `controlledRenderers`
  - `nonRoomRoots` 配下にあり、Runtime で `Renderer.enabled` を切り替える対象
- `excludedRenderers`
  - 常時表示扱いで、Runtime が毎回 `enabled = true` を入れる対象
- `roomRenderers`
  - 各 `roomRoot` 配下で `Renderer.enabled` を切り替える対象
- `controlledTerrainObjects`
  - `nonRoomRoots` 配下の Terrain GameObject
- `controlledCanvases`
  - `nonRoomRoots` 配下にあり、Runtime で `Canvas.enabled` を切り替える対象
- `roomCanvases`
  - 各 `roomRoot` 配下で `Canvas.enabled` を切り替える対象
- `controlledLights`
  - `nonRoomRoots` 配下にあり、Runtime で `Light.enabled` を切り替える対象
- `roomLights`
  - 各 `roomRoot` 配下で `Light.enabled` を切り替える対象
- `controlledActiveObjects`
  - `nonRoomRoots` 配下で `SetActive` を切り替える対象
- `roomActiveObjects`
  - `roomRoots` 配下で `SetActive` を切り替える対象

## 5. 切替ルール

Runtime の `ApplyVisibility()` は次の順で処理します。

1. `controlledRenderers` を `室外` 列の設定で切り替える
2. `roomRenderers` を「現在位置の行 × 各部屋列」の設定で切り替える
3. `excludedRenderers` を強制的に表示に戻す
4. `controlledCanvases` / `roomCanvases` / `excludedCanvases` を `Canvas.enabled` で切り替える
5. `controlledLights` / `roomLights` / `excludedLights` を `Light.enabled` で切り替える
6. `controlledTerrainObjects` を `室外` 列の設定で `SetActive`
7. `controlledActiveObjects` を `室外` 列の設定で `SetActive`
8. `roomActiveObjects` を部屋列の設定で `SetActive`

要点は次のとおりです。

- `nonRoomRoots` 配下の対象は「室外列」を見る
- `roomRoots` 配下の対象は「各部屋列」を見る
- `visibilityExcludeRoots` 配下は原則この制御から外す
- ただし `excludedRenderers` / `excludedCanvases` / `excludedLights` は毎回 `enabled = true` を入れるため、別処理が同じ対象を触る構成とは相性が悪い

## 6. セットアップ手順

1. 空の GameObject に `RoomVisibilityManager` を追加する
2. `roomRoots` に各部屋のルートを入れる
3. `nonRoomRoots` に部屋外として扱いたい親を入れる
4. 必要なら `visibilityExcludeRoots` を入れる
5. 必要なら `activeToggleRoots` を入れる
6. 必要なら `controlTerrains` を ON にする
7. `roomHiddenMatrix` を調整する
8. `セットアップ実行` を押す
9. 生成された `roomBoundingBoxes` を必要に応じて調整する

部屋構成を変えたときは、基本的に再度 `セットアップ実行` が必要です。

## 7. Inspector で気を付ける点

- `roomHiddenMatrix` は部屋数変更時に自動リサイズされる
- 既存行列が正方行列なら、重なる範囲の値は引き継がれる
- 対角セルは常に表示 ON で固定
- `checkInterval` は 0 より大きい必要がある
- `startupDelaySeconds` は 0 以上が必要
- `boundsHysteresisMargin` は 0 以上が必要
- `enableDebugLogs` は Inspector 最下部の `デバッグ` セクションにある

## 8. 実装上の制約と癖

### 8.1 複数 Manager 併用

正式対応していません。  
Editor では同一シーン内に有効な `RoomVisibilityManager` が複数ある時点で警告を出します。  
さらに、同一シーン内の別 `RoomVisibilityManager` のバウンディングボックスと重なった場合にも警告を出します。  
ただし、相手の Manager が無効状態、非アクティブ、または `EditorOnly` 配下にある場合は警告対象から外します。  
Runtime で複数 Manager を協調制御する仕組みはありません。

### 8.2 部屋の重なり

未対応です。  
`DetermineCurrentRoomIndex()` は `roomBoundingBoxes` を先頭から順に見て、最初にヒットした部屋を返します。重なり時の優先順位制御はありません。  
Editor のセットアップ時には、同一 Manager 内で重なっている部屋同士を警告します。  
`boundsHysteresisMargin` が `0` のときはヒステリシスがありません。  
`0` より大きい場合は、「現在いる部屋に留まる判定」だけ `boundsMargin + boundsHysteresisMargin` を使います。境界付近での出入り判定の揺れを抑えるためのものです。

### 8.3 `activeToggleRoots` と除外設定の衝突

`visibilityExcludeRoots` の子が `activeToggleRoots` の配下にあると、親の `SetActive(false)` に巻き込まれます。Renderer / Canvas / Light いずれでも同じです。  
Editor はこの構成を検出して警告を出します。

### 8.4 inactive オブジェクトの扱い

- `includeInactiveRenderers` は主に Editor 側の探索で使う
- ただし Runtime の制御対象に採用されるのは、最終的に `activeSelf` / `activeInHierarchy` / `renderer.enabled` を満たすものだけ
- つまり「inactive のまま保持したいもの」を自動で復帰させる設計ではない

### 8.5 Terrain 制御

Terrain は `drawHeightmap` ではなく Terrain GameObject 自体の `SetActive` で切り替えます。

### 8.6 旧設定の残骸

次の 2 つは Runtime で実質未使用です。

- `outsideRoomBehaviour`
- `hideRoomsWhenOutside`

互換維持用で残っているだけと考えてよいです。

### 8.7 `reuseExistingBoundingBoxes`

Inspector に項目はありますが、現状の `GenerateBoundingBoxes()` は既存 `roomBoundingBoxes` をいったん削除して新規生成します。  
名前どおりの「再利用」はまだ実装されていません。

## 9. バウンディングボックス生成仕様

各部屋について次の順で Bounds を取ります。

1. 配下の `Renderer`
2. Renderer が取れなければ配下の `Collider`

除外条件は次のとおりです。

- `boundsIgnoreRoots` 配下
- すでに生成済みの `RoomVisibilityBoundingBox` 配下
- `EditorOnly` 配下

生成される `BoxCollider` は

- `isTrigger = true`
- ワールド座標系基準の Bounds を使う
- 親の `lossyScale` を打ち消すローカルスケールで配置する

そのため、`boundingBoxParent` 側にスケールが入っていても見た目の大きさが崩れにくい実装です。

`boundsMargin` と `boundingBoxMargin` の違いは次のとおりです。

- `boundsMargin`
  - Runtime の `IsInsideBoundingBox()` でだけ使う判定余裕
  - 既存の `BoxCollider` 自体のサイズは変えない
- `boundsHysteresisMargin`
  - Runtime で「現在いる部屋に留まるか」を判定するときだけ `boundsMargin` に加算される余裕
  - 入室判定より退室判定を少し緩くして、境界でのチラつきを抑える
- `boundingBoxMargin`
  - Editor の `GenerateBoundingBoxes()` で自動生成するときに使う余白
  - 生成される `BoxCollider` 自体のサイズが大きくなる

## 10. Editor ボタンの安全策

`セットアップ実行` 前後で関連 Renderer の `enabled` 状態を退避・復元しています。  
Editor 実行によって既存の見た目を壊しにくくするためです。Undo も記録されます。

ただし Runtime 中は普通に次を変更します。

- `Renderer.enabled`
- `Canvas.enabled`
- `Light.enabled`
- `GameObject.SetActive`

他コンポーネントが同じ対象を同時に制御する構成は避けたほうが安全です。

## 11. Runtime との差分を避けるための前提

- `EditorOnly` タグ配下のオブジェクトは、セットアップ時の部屋一覧・表示制御対象・Bounds 計算から除外されます
- これは VRChat / ClientSim 実行時に `EditorOnly` オブジェクトが削除され、Editor で作ったキャッシュと Runtime の実体がずれないようにするためです
- `roomRoots` に `EditorOnly` 配下のオブジェクトを入れても、Runtime では部屋として扱われません

## 12. Runtime ログ方針

- `RoomVisibilityManager` は起動時に 1 行の要約ログを常に出します
- `roomBoundingBoxes` 未設定、部屋数と Bounding Box 数の不一致、表示制御表サイズ不一致は warning として常時出します
- `enableDebugLogs` を ON にしたときだけ、部屋判定と表示切替の詳細トレースを追加で出します
- `enableDebugLogs` が ON の間は、1 秒ごとに「現在地」と「室外 + 各部屋の表示 / 非表示状態」のスナップショットも出します

## 13. 運用メモ

- コードを直したら、README も Runtime / Editor の責務差分がないか確認する
- 特に `GenerateBoundingBoxes()` と `RebuildCache()` の仕様変更は README へ反映する
- Inspector 表示名は日本語、コード上の配列名は英語なので、保守時は両方の対応を意識する

## 14. 次回触るときの確認ポイント

最初に見るべき箇所は次の順です。

1. `RoomVisibilityManager.cs`
   - Runtime で実際に何を切り替えるか
2. `Editor/RoomVisibilityManagerEditor.cs`
   - セットアップ時にどの配列をどう生成するか
3. `readme.md`
   - 想定仕様と既知制約

README とコードで食い違いがあれば、コードを正として README を更新する前提で扱うのが安全です。
