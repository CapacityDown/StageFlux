# Graphical HUD Design

## 1. Scope

Stage Fluxのステージ中HUDを、既存のテキストHUDと切り替え可能なグラフィカルHUDへ拡張する。

- デフォルトはグラフィカルHUD
- デフォルトレイアウトは縦型
- 現行テキストHUDへ設定で戻せる
- グラフィカルHUDは常に5スロットを表示する
- `MaxSimultaneousEffects`を超えるスロットには鍵を表示する
- 同時発生したエフェクトは共通のアナログストップウォッチを1個だけ使用する
- 数字による残り秒数は表示しない
- 待機中は次回エフェクト群、発動中は各エフェクトの危険度を色で示す
- アイコン切り替え時にアニメーションする
- HUDはプレイ可能なステージ中だけ表示する

承認用モックアップ:

- `Assets/UIConcepts/stage-flux-graphical-hud-concept-vertical.png`
- `Assets/UIConcepts/stage-flux-graphical-hud-concept-v2.png`

## 2. Configuration

### New settings

| Section | Key | Type | Default | Values | Purpose |
|---|---|---:|---|---|---|
| HUD | Style | string | `Graphical` | `Graphical`, `Classic` | グラフィカルHUDと現行テキストHUDを切り替える |
| HUD | LayoutDirection | string | `Vertical` | `Vertical`, `Horizontal` | グラフィカルHUDの配置方向を切り替える |
| HUD | BackgroundOpacityPercent | int | `50` | `0`～`100` | グラフィカルHUD背景の不透明度。アイコンと文字には適用しない |

### Existing settings

| Key | Graphical | Classic |
|---|---|---|
| Enabled | 使用 | 使用 |
| Anchor | 使用 | 使用 |
| OffsetX | 使用 | 使用 |
| OffsetY | 使用 | 使用 |
| ScalePercent | 使用 | 使用 |
| BackgroundOpacityPercent | 使用 | 使用しない |
| Alignment | 使用しない | 現行どおり使用 |

`LayoutDirection`は`Style = Classic`のとき無視する。

`MaxSimultaneousEffects`はHUD設定ではなく、従来どおり`General`設定を使用する。値は1～5で、グラフィカルHUDの物理スロット数そのものは常に5とする。

### Runtime slot limit

ホストの`MaxSimultaneousEffects`を1～5へクランプし、ステージ中も変更を監視する。

- 変更後は鍵の位置を即時更新し、Room PropertiesでMOD導入済み参加者へ同期する
- 新しい上限は次回の未確定抽選から使用する
- 進行中または確定済みイベントの数が新上限を超える場合、そのイベントが終了するまでは必要な枠を一時的に維持する
- 物理スロット数は常に5個のままとし、GameObjectを作り直さない

## 3. Presentation model

### HUD styles

#### Graphical

- 5個の固定スロット
- フレームを持たないイベントモチーフ
- 危険度色を持つHUD側のスロット外枠
- 共有アナログストップウォッチ
- R.E.P.O. UIフォント
- マットな色、太い黒縁、暗い半透明背景

#### Classic

現在の`StagePhysicsEventHud.BuildText`相当をそのまま維持する。

- Waiting / Interval
- Startingフェーズは使用しない
- Effect names / Duration
- Persistent mode

既存ユーザーが`Style = Classic`を選んだ場合、見た目と文字列を変更しない。

### Fixed five-slot rules

スロット番号は常に1～5。

1. 発動中またはカウントダウン中のエフェクトを、`StageEffectSet.IndividualEffects`の順で詰める
2. `_stageSlotLimit`以内で未使用のスロットは空スロットとして表示する
3. `_stageSlotLimit`を超えるスロットは鍵アイコンを表示する
4. エフェクト数が変わってもスロットGameObjectは作り直さない
5. 縦型と横型で同じ5個のSlotViewを並べ替える

例: `_stageSlotLimit = 3`かつ2エフェクト発動中

| Slot | Display |
|---:|---|
| 1 | Effect icon |
| 2 | Effect icon |
| 3 | Empty |
| 4 | Locked |
| 5 | Locked |

## 4. State-specific display

### Inactive

HUD全体を非表示にする。

- ステージ抽選に失敗した場合
- プレイ可能なステージ外
- ステージ生成前
- ショップ、アリーナ、チュートリアル
- `HUD.Enabled = false`

### Waiting

- HUD全体はInterval中も表示する
- 残り10秒より前はSlot 1をWaiting、残りの使用可能枠をEmptyとして表示する
- 残り10秒で抽選済みイベントアイコンへ回転し、上または左から順に停止する
- Interval開始時点で残り10秒以下ならWaitingアイコンを省略し、イベント抽選表示へ直接進む
- 残り10秒で次回エフェクト、効果時間、次回インターバルを抽選する
- 抽選後は各イベント枠へ個別の危険度色を使用する
- `_stageSlotLimit + 1`～5: Locked
- 共有ストップウォッチ: 現在のインターバル進行
- 次回抽選結果が`None`の場合はWaiting／Empty表示を維持する
- Active移行時は確定済みイベントアイコンを引き継ぎ、再スピンしない
- Starting/Countdown状態へは遷移せず、カウントダウン中もWaitingを維持する
- 開始カウントダウン有効時は残り8秒でエフェクト名、残り5秒から`5`～`0`をチャット通知する
- 開始カウントダウン無効時は残り3秒でエフェクト名だけを通知する
- Interval終了と同時にActiveへ直接遷移する

### Active

- 各発動エフェクトを1スロットずつ表示
- 各スロットの枠色はそのエフェクトの危険度
- 共有ストップウォッチは共通効果時間を示す
- 終了カウントダウン中も同じ表示を継続する

### PersistentForStage

- 発動エフェクトは通常どおり表示
- ストップウォッチは300秒の再適用サイクルを表示する
- ストップウォッチへ小さな`∞`マークを重ね、「終了時間」ではなく継続モードであることを示す
- 再適用時は針を先頭へ戻すが、イベントアイコンの切り替えアニメーションは行わない

## 5. Danger levels

### Model

```csharp
internal enum EventDangerLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}
```

複数エフェクトの総合危険度は、含まれる個別危険度の最大値とする。

```csharp
aggregate = Max(GetDanger(effect) for each selected effect)
```

待機中のWaiting枠は総合危険度を使用する。発動中は各イベントの個別危険度を各スロットへ使用する。

### Color palette

| Level | Color | Hex | Additional cue |
|---|---|---|---|
| None | Neutral gray | `#B0B3AA` | notchなし |
| Low | Muted green | `#70A650` | 1 notch |
| Medium | Mustard amber | `#CF9E35` | 2 notches |
| High | Muted red | `#B84143` | 3 notches |
| Locked | Dark gray | `#5B5E5B` | padlock |

色覚差を考慮し、色だけでなく外枠上部のnotch数でも危険度を区別する。

### Default danger mapping

#### Low

- Feather
- Battery Charge
- Heal
- Indestructible
- Enemy Purge
- Second Chance
- Value Surge
- Healing Aura

#### Medium

- Zero Gravity
- Levitation
- Freeze
- Stun
- Shockwave
- Stun Blast
- Knockback
- Flicker
- Door Chaos
- Gumball Hypnosis
- Spider Scare

#### High

- Fragility
- Roll
- Void
- Explosion Rain
- Enemy Wave
- Minefield
- Enemy Warp
- Enemy Hunt
- Enemy Regen
- Damage Pulse
- Quake
- Value Crash
- Star Barrage
- Traffic Shock
- Dangerous Valuables

危険度はUIメタデータであり、ゲーム処理や抽選確率には影響させない。

## 6. Waiting preview and event planning

現行の`RandomEachEvent`は待機終了時に`RollEffects()`を実行する。このままでは待機中に次回危険度を表示できない。

### New planning field

```csharp
private StageEffect _plannedEffect;
```

### New flow

1. `BeginWaiting`では予定値を未確定へ戻す
2. Interval残り10秒で`PrepareIntervalPlan()`を実行
3. `RandomEachEvent`では、その時点で`RollEffects(_stageSlotLimit)`を実行
4. Fixed系モードでは`_fixedEffect`を使用
5. Waiting中は`_plannedEffect`の総合危険度だけを表示
6. Waiting終了時は再抽選せず、`_plannedEffect`を`BeginEvent`へ渡してActiveへ直接遷移する
7. `_plannedEffect == None`なら、現行仕様どおり効果を発動せず次のWaitingへ移る

抽選確率、競合排除、最大同時数の規則は変更しない。乱数を引く時刻だけをInterval残り10秒へ移す。

### FixedPerExtraction

納品完了時に`_fixedEffect`を更新する。残り10秒の計画が未確定なら現在のIntervalに使用し、確定済みなら次のIntervalから使用する。進行中イベントは変更しない。

## 7. Multiplayer synchronization

バニラ参加者には従来どおりチャットだけが見える。HUD同期対象はMOD導入済み参加者だけ。

### State format

`StateFormatVersion`を10から11へ更新する。

### Additional room properties

| Key | Type | Purpose |
|---|---|---|
| `SPE.PreviewEffect` | long | Waiting中に予定されているエフェクト群 |
| `SPE.SlotLimit` | int | ホストの現在の同時発生上限。表示中イベント数が上限を超える場合は必要枠数 |
| `SPE.PhaseDurationSeconds` | int | 現在のアナログ時計の分母 |
| `SPE.Mode` | int | Persistent表示を含む解決済みEventMode |

### Extended HudState

```csharp
internal readonly struct HudState
{
    EventRunState State;
    StageEffect Effect;
    StageEffect PreviewEffect;
    EventMode Mode;
    int DurationSeconds;
    int IntervalSeconds;
    int PhaseDurationSeconds;
    int RemainingSeconds;
    int SlotLimit;
}
```

クライアント側の`MaxSimultaneousEffects`は鍵表示に使用しない。必ずホスト同期値を使用する。

`HUD.Style`、`HUD.LayoutDirection`、Anchor、Offset、Scaleは各クライアントのローカル設定を使用できる。

## 8. Analog stopwatch

### Progress

```csharp
fraction = Clamp01(RemainingSeconds / (float)PhaseDurationSeconds);
angle = -360f * (1f - fraction);
```

- 数字の秒数は表示しない
- 針は時計回り
- 外周の残量弧も同じfractionで減少
- `Time.unscaledDeltaTime`で補間し、時間操作系の影響を受けない
- State変更時は0.15秒で新しい角度へ補間する

### Phase duration

| State | PhaseDurationSeconds |
|---|---:|
| Waiting | IntervalSeconds |
| Active | DurationSeconds |
| Persistent refresh | 300 |

## 9. Icon assets

### Runtime format

- 35 PNG files: Waiting + 34 events
- 256 x 256
- RGBA transparent background
- 中央モチーフのみ
- アイコン固有のメダリオン、円形リング、三色ノードを含めない
- 10%程度の安全余白
- R.E.P.O.に合わせたマットな質感と太い暗色アウトライン
- 文字を画像へ含めない

### Project layout

```text
Assets/EventIcons/Runtime/
  Waiting.png
  Feather.png
  ...
  DangerousValuables.png
```

### Embedding

PNGをDLLのEmbeddedResourceとして格納する。

```xml
<EmbeddedResource
  Include="Assets\EventIcons\Runtime\*.png"
  LogicalName="StagePhysicsEvents.Assets.EventIcons.%(Filename)%(Extension)" />
```

`EventIconCatalog`がAssembly resource streamから`Texture2D`を生成し、キャッシュする。

- 生成したTextureへ`HideFlags.HideAndDontSave`
- Bilinear filtering
- Clamp wrap mode
- HUD破棄時にTextureをDestroy
- 読み込み失敗時はイベント名の先頭文字を表示し、HUD全体は停止しない

## 10. UI architecture

### StagePhysicsEventHud

Facadeとして以下を管理する。

- stage visibility
- font discovery
- local HUD settings
- current HudState
- Classic / Graphical view selection

### ClassicHudView

既存のTextMeshProUGUI生成、`BuildText`、Alignment処理を移動する。

### GraphicalHudView

```text
GraphicalHudRoot
├─ Header
├─ SlotContainer
│  ├─ SlotView[0]
│  ├─ SlotView[1]
│  ├─ SlotView[2]
│  ├─ SlotView[3]
│  └─ SlotView[4]
└─ AnalogStopwatchView
```

各`SlotView`:

```text
SlotView
├─ Backplate
├─ DangerBorder
├─ DangerNotches
├─ Icon
├─ LockIcon
└─ Label
```

### Layout

#### Vertical

- デフォルト
- Header、5スロット、共有時計を上から下へ配置
- BottomRight anchorでは下端を基準に上方向へ伸ばす
- Right系anchorではラベルをアイコン左側へ出し、画面外にはみ出さない
- Left系anchorではラベルをアイコン右側へ出す

#### Horizontal

- 5スロット、共有時計を横一列
- Right系anchorでは右から左へ伸ばす
- Left系anchorでは左から右へ伸ばす
- Center系anchorでは中央揃え

既存の`HudAnchor`、Offset、Scaleをrootへ適用し、個々のスロットへは適用しない。

## 11. Animation

外部Tweenライブラリは追加しない。`Time.unscaledDeltaTime`を使用する。

### Slot-reel replacement

イベント切り替え時は、スロットマシンのリールを模した縦回転を使用する。

1. 切り替え対象となる使用可能スロットが同時に回転を開始
2. 回転中は候補モチーフを約75ms間隔で切り替え、上下移動と弱いmotion blurを付ける
3. 最初のスロットは600ms後から減速して停止
4. 以降のスロットは180ms間隔で順番に停止
5. 停止時は目的のアイコン、ラベル、危険度へ確定
6. 最後に9%程度の小さな上下overshootを入れ、220msで静止
7. DangerBorderは各スロットが停止した時点で180msかけて新しい色へ変化

停止順:

- `Vertical`: 上から下
- `Horizontal`: 左から右

Lockedスロットは回転せず、常に静止したままとする。

### State transitions

- Waiting残り10秒:
  - Slot 1～使用可能枠が同時に回転開始
  - 上または左から順に予定エフェクト、Emptyへ停止
- Waiting → Active:
  - Interval中に確定した表示を維持し、再回転しない
- Active → Waiting:
  - 全使用可能枠を同時に動かし、現在のアイコンを回転と同じ移動速度で枠外へ送る
  - Slot 1はWaiting、残りはEmptyを同時に枠内へスライドする
- Empty → Effect / Effect → Empty:
  - 同じslot-reel animationを使用
- Persistent refresh:
  - アイコンは回転させず、時計だけを先頭へ戻す
- LayoutDirectionのローカル変更:
  - 配置だけを即時変更し、アイコン回転は行わない

アニメーションは表示処理のみで、エフェクト開始・終了やチャット通知を待機させない。ゲーム効果はInterval終了と同時に開始し、HUDのリールだけが独立して完了する。

### Animation cancellation

新しいHudStateが届いた場合は各スロットのgenerationを更新し、古いアニメーションを安全に中断する。

## 12. Rendering and lifecycle

- 親レイヤーは現行どおり`HealthUI.instance.transform.parent`を優先
- `HealthUI`の直後のSiblingIndexを使用
- `raycastTarget = false`
- R.E.P.O. UIフォントは現行の`TryApplyRepoUiFont`を共有
- `StageEnding`、scene change、HUD destroyでrootとTextureを解放
- ステージ外ではrootを非表示
- 画面解像度変更時はUnity anchorに追従
- 毎フレーム文字列やGameObjectを生成しない
- 状態signatureが変化したときだけスロット内容を更新
- 毎フレーム更新するのは時計の針と残量弧のみ

## 13. Compatibility

- ホストのみ導入時のゲーム効果とチャット通知は変更しない
- MOD未導入参加者に追加RPCやPrefabを要求しない
- Room Custom PropertiesだけでHUD情報を同期する
- DroneToOrbItemやアイテム化されたオーブの処理には触れない
- Classic HUDを残し、既存のAnchor、Alignment、Offset、Scale設定を維持する
- 新設定が存在しない既存configでは、`Graphical`かつ`Vertical`が自動的に使用される
- 旧`UI`セクションの移行処理は維持する

## 14. Implementation order

1. `HudStyle`と`LayoutDirection`設定を追加
2. `_stageSlotLimit`とplanned event flowを追加
3. state format 11と追加Room Propertiesを実装
4. `HudState`を拡張
5. 現行HUDを`ClassicHudView`へ分離
6. `EventPresentationCatalog`と危険度マップを追加
7. frameless runtime iconsを完成させEmbeddedResource化
8. `GraphicalHudView`、5スロット、鍵、空スロットを実装
9. 共有アナログストップウォッチを実装
10. 縦型／横型layoutを実装
11. icon transition animationを実装
12. singleplayer、host、modded client、vanilla clientで検証

## 15. Verification matrix

### States

- Stage activation failed: HUDなし
- Waiting with planned effect
- Waiting with `None`
- Start countdown enabled / disabled
- Active with 1～5 effects
- End countdown enabled / disabled
- PersistentForStage refresh
- FixedForStage
- FixedPerExtraction preview update
- AllModeで選ばれた全4モード

### Layout and settings

- Graphical / Classic
- Vertical / Horizontal
- all 9 anchors
- Scale 50 / 70 / 100 / 200
- Offset positive / negative
- MaxSimultaneousEffects 1～5
- configをステージ中に変更し、次ステージから反映されること

### Multiplayer

- Singleplayer
- Host only mod + vanilla participant
- Host and one modded participant
- Host and multiple modded participants with different local HUD layouts
- Late join during Waiting / Countdown / Active
- Room property format mismatch

### Visual

- 35 iconsの読み込み
- missing resource fallback
- danger color and notch mapping
- icon change animation interruption
- stopwatch reset and phase transition
- stage end cleanup
