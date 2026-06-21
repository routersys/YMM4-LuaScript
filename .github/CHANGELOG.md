# v1.0.0 - Lua スクリプト for YMM4

YukkuriMovieMaker4 向けの Lua スクリプトエフェクトプラグインの初回リリースです。
AviUtl アニメーション効果互換の変数・関数体系を YMM4 映像エフェクトとして提供し、
MoonSharp による Lua 実行エンジン・専用コードエディター・シンタックスハイライト・
折り畳み・オートコンプリート・ファイルインポート/エクスポート・ピクセル直接操作・
独自アニメーションユーティリティライブラリ・8 言語対応 UI を備えた映像エフェクトプラグインです。

---

## 新機能

### 1. エフェクト定義（LuaScriptEffect）

`LuaScriptEffect` は `VideoEffectBase` を継承し、`IScriptProvider` を実装します。

`[VideoEffect]` 属性は以下のパラメーターで宣言されます。

- 表示名：`Texts.LuaScript`（ローカライズキー）
- カテゴリー：`VideoEffectCategories.Filtering`
- 検索タグ：`"lua"`・`"script"`・`"スクリプト"`・`"lua script"`・`"アニメーション効果"`・`"animation"`
- `IsAviUtlSupported = false` により AviUtl 向け EXO 出力は非対応です。
- `ResourceType = typeof(Texts)` でローカライズリソースを指定します。

`Label` プロパティは `Texts.LuaScript` の固定値を返却します。

デフォルトスクリプトは以下のとおりです。

```lua
obj.alpha = math.min(time * 255, 255)
```

公開プロパティは以下のとおりです。

- `ToolBar`（`object?`、常に `null`）:
  `[LuaScriptToolBar]` でインポート・エクスポート・クリアのツールバーを表示します。
  `Texts.ScriptGroup` グループに属します。

- `Script`（`string`、デフォルト `DefaultScript`）:
  `[CodeEditor]` でシンタックスハイライト付きコードエディターとして表示されます。
  シンタックス定義は `pack://application:,,,/LuaScript;component/Resources/SyntaxDefinitions/Lua-{theme}.xshd`
  をテーマ別に読み込みます。折り畳みに `LuaFoldingStrategy`、オートコンプリートに
  `LuaAutoCompletionStrategy` を使用します。`PropertyEditorSize.FullWidth` で全幅表示されます。
  `Set` による変更通知を伴うフィールドプロパティとして実装されます。
  `Texts.ScriptGroup` グループに属します。

- `Track0`〜`Track3`（`Animation`、デフォルト `0`、内部範囲：`YMM4Constants.VerySmallValue`〜`YMM4Constants.VeryLargeValue`）:
  `[AnimationSlider("F2", "", -100, 100)]` でスライダー操作範囲 -100〜100 として表示されます。
  スクリプト内では `obj.track0`〜`obj.track3` でアクセスします。
  AviUtl の `track0`〜`track3` に対応するスライダー値です。
  `Texts.ParametersGroup` グループに属します。

`CreateExoVideoFilters` は空の `IEnumerable<string>` を返却し、EXO 向けフィルター出力は実装されていません。

`CreateVideoEffect(IGraphicsDevicesAndContext devices)` は
`new LuaScriptEffectProcessor(devices, this)` を返却します。

`GetAnimatables` は `Track0`・`Track1`・`Track2`・`Track3` を列挙します。

---

### 2. スクリプト実行コンテキスト（AviUtlScriptContext）

`AviUtlScriptContext` はスクリプト実行時の状態を保持する内部クラスです。

#### 読み取り専用プロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `ImageWidth` | `int` | 画像幅（ピクセル） |
| `ImageHeight` | `int` | 画像高さ（ピクセル） |
| `Track0`〜`Track3` | `double` | スライダー値 |
| `Time` | `double` | アイテム内の経過時間（秒） |
| `Frame` | `int` | アイテム内の経過フレーム数 |
| `TotalFrame` | `int` | アイテムの総フレーム数 |
| `TotalTime` | `double` | アイテムの総時間（秒） |
| `Framerate` | `int` | フレームレート |
| `TimelineFrame` | `int` | タイムライン上の経過フレーム数 |
| `TimelineTime` | `double` | タイムライン上の経過時間（秒） |
| `SceneWidth` | `int` | シーン幅 |
| `SceneHeight` | `int` | シーン高さ |
| `Layer` | `int` | レイヤー番号 |
| `Index` | `int` | グループ内のインデックス |
| `Num` | `int` | グループ内の総数 |
| `GroupIndex` | `int` | グループインデックス |
| `GroupCount` | `int` | グループ総数 |
| `TimelineTotalFrame` | `int` | タイムライン総フレーム数 |
| `TimelineTotalTime` | `double` | タイムライン総時間（秒） |
| `IsSaving` | `bool` | エクスポート中かどうか |
| `IsPlaying` | `bool` | 再生中かどうか |
| `IsPaused` | `bool` | 一時停止中かどうか |
| `TimeRatio` | `double` | アイテム内の時間比率（Frame / TotalFrame） |
| `SceneId` | `string` | シーン ID |

#### 書き換え可能プロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `X`・`Y`・`Z` | `double` | 描画位置 |
| `Ox`・`Oy`・`Oz` | `double` | 回転中心のオフセット |
| `Sx`・`Sy` | `double` | X・Y 方向の拡大率 |
| `Zoom` | `double` | 拡大率（(Sx + Sy) / 2） |
| `Aspect` | `double` | アスペクト比（(Sx - Sy) / (Sx + Sy)） |
| `Alpha` | `double` | 不透明度（0〜255） |
| `Rx`・`Ry`・`Rz` | `double` | X・Y・Z 軸回転角（度） |

`RxRad`・`RyRad`・`RzRad` は各 `Rx`・`Ry`・`Rz` をラジアンに換算した計算プロパティです。

#### ピクセル操作

- `GetPixel(int x, int y)` は `(r, g, b, a)` のタプルを返却します。各成分は 0〜255 の `double` で、
  プリマルチプライ済み BGRA からストレートアルファの RGB へ復元して返却します。
  アルファが 0 以下の場合はすべて 0 を返却します。
- `SetPixel(int x, int y, double r, double g, double b, double a)` は
  ストレートアルファの RGB をプリマルチプライ済み BGRA に変換してバッファーへ書き込みます。
  `a` のデフォルトは 255 です。書き込み後は `IsPixelsDirty` が true になります。
- `EnsurePixelBuffer` はピクセルローダーを遅延呼び出しして入力画像をバッファーへ読み込みます。
- `GetPixelBuffer` は内部の `byte[]` バッファーを返却します。

---

### 3. エフェクトプロセッサー（LuaScriptEffectProcessor）

`LuaScriptEffectProcessor` は `VideoEffectProcessorBase` を継承し、
コンストラクターで `IGraphicsDevicesAndContext devices` と `LuaScriptEffect item` を受け取ります。

#### CreateEffect メソッド

`GraphicsDevicesAndContext` のプライベートコンテキスト `_ownCtx` を生成して `disposer` に登録し、
`null` を返却します。出力画像の生成はスクリプト実行後に動的に決定します。

#### setInput / ClearEffectChain メソッド

`setInput` は入力が前回と異なる場合に `_isFirst = true` としてキャッシュを無効化します。
`ClearEffectChain` は `effectOutput` を null に設定します。

#### Update メソッド

`Update(EffectDescription desc)` は毎フレーム呼び出されます。

`input` または `_ownCtx` が null の場合は `desc.DrawDescription` をそのまま返却します。

`RenderKey` レコードでフレーム・時間・長さ・FPS・スライダー値 4 本・スクリプト文字列・
利用用途・シーン ID・タイムライン情報・シーンサイズ・レイヤー・インデックス・グループ情報・
タイムライン総フレーム数・`DrawDescription` を一括保持し、
前回と同一の場合はキャッシュした結果をそのまま返却します。

スクリプト実行時は `BuildContext` で `AviUtlScriptContext` を生成し、
`SetPixelLoader` でピクセル遅延ローダーを登録してから `LuaScriptEngine.Execute` を呼び出します。
実行後に `ctx.IsPixelsDirty` が true の場合のみ出力ビットマップへ書き込み、
`AffineTransform2D` で入力境界の原点へ平行移動して `effectOutput` に設定します。
ピクセルが変更されていない場合は `effectOutput = null` としてパススルーします。

`BuildOutputDesc` が書き換え後の `ctx.X/Y/Z/Ox/Oy/Sx/Sy/Zoom/Aspect/Alpha/Rx/Ry/Rz` から
新しい `DrawDescription` を構築して返却します。`Alpha` は `ctx.Alpha / 255d` を
0〜1 にクランプします。

例外は `LuaScriptException` を捕捉してログに書き込み、エラー時は入力の `DrawDescription` を
そのまま返却します。タイムアウトは `LuaScriptRuntimeException` として処理されます。

#### ビットマップ管理

`EnsureBitmaps(int width, int height)` でサイズ変化時にビットマップ 3 枚と変換エフェクトを再生成します。

- `_renderTarget`：`BitmapOptions.Target`、入力を描画する作業バッファー
- `_stagingBitmap`：`B8G8R8A8_UNorm`・`Premultiplied`・`CpuRead | CannotDraw`、CPU 読み出し用
- `_outputBitmap`：`BitmapOptions.Target`、ピクセル操作後の書き出し先

`LoadInputPixels` は `_ownCtx` の独立デバイスコンテキストで入力を `_renderTarget` へ描画し、
`_stagingBitmap` へコピーして `Map(MapOptions.Read)` で `_pixelBuffer` へ読み出します。
ピッチが行ストライドと一致する場合は一括コピー、そうでない場合は行ごとにコピーします。

---

### 4. Lua 実行エンジン（LuaScriptEngine）

`LuaScriptEngine` は `IDisposable` を実装します。

タイムアウトは `ExecutionTimeoutMilliseconds = 5000`（5 秒）です。

タイムアウト時は実行スレッドを非同期で破棄して新しいスレッドを生成し、
`LuaScriptRuntimeException` をスローします。

#### ExecutionThread 内部クラス

Lua スクリプトを専用ワーカースレッドで逐次実行する仕組みです。
キューのバウンド容量は 1 で、実行要求が溢れた場合は即座にエラーを返却します。

`CancellationDebugger` により、キャンセルトークンを MoonSharp デバッガーフックへ橋渡しし、
タイムアウト時に `OperationCanceledException` を発生させます。

コンパイル済みチャンクはスクリプト文字列が変化した場合のみ `LoadString` で再コンパイルします。
再コンパイル時の構文エラーは `LuaScriptCompilationException` としてラップします。
実行時エラーは `LuaScriptRuntimeException` としてラップします。

`EnsureScriptIntegrity` は `math` グローバルが消失していた場合に Lua VM を完全リセットします。

有効な Lua モジュールは以下のとおりです。

`Basic` / `Math` / `String` / `Table` / `Bit32` / `TableIterators` / `Metatables` / `ErrorHandling`

ファイルアクセスは `DisabledFileScriptLoader` により無効化されています。

フレームシードによる `math.randomseed` 呼び出しにより、同一フレームでは `math.random` が
再現性のある値を返します。

#### SetupGlobals メソッド

スクリプト実行前にグローバル変数と各テーブルを初期化します。
ユーザーがフレーム間でグローバルに書き込んだキーは次フレームの実行前に除去されます
（`_builtinGlobalSnapshot` との差分を削除）。
各テーブル（`obj`・`scene`・`anim`・`ymm4`）も同様にフレーム間でリセットします。

スクリプトから利用可能なグローバル変数は以下のとおりです。

| 変数名 | 型 | 説明 |
|---|---|---|
| `time` | `double` | アイテム内の経過時間（秒） |
| `frame` | `int` | アイテム内の経過フレーム数 |
| `totalframe` | `int` | アイテムの総フレーム数 |
| `framerate` | `int` | フレームレート |
| `timelineframe` | `int` | タイムライン上の経過フレーム数 |
| `timelinetime` | `double` | タイムライン上の経過時間（秒） |
| `layer` | `int` | レイヤー番号 |

#### ReadBackGlobals メソッド

スクリプト実行後に `obj` テーブルの変数を `AviUtlScriptContext` へ書き戻します。
書き戻し対象は `x`・`y`・`z`・`ox`・`oy`・`oz`・`alpha`・`sx`・`sy`・`zoom`・`aspect`・
`rx`・`ry`・`rz`・`rxr`・`ryr`・`rzr` です。

`zoom`・`aspect` が変化し、かつ `sx`・`sy` が変化していない場合は
`sx = zoom * (1 + aspect)`・`sy = zoom * (1 - aspect)` で `sx`・`sy` を再計算します。

`rxr`・`ryr`・`rzr` が前回値から変化していた場合は、それをラジアンから度に変換して
`rx`・`ry`・`rz` を上書きします。それ以外の場合は `rx`・`ry`・`rz` を直接読み返します。

---

### 5. スクリプト API（obj テーブル）

スクリプトから参照・書き換えできる `obj` テーブルのメンバーは以下のとおりです。

#### 読み取り専用メンバー

| メンバー | 説明 |
|---|---|
| `obj.w` | 画像幅（ピクセル） |
| `obj.h` | 画像高さ（ピクセル） |
| `obj.hw` | 画像幅の半分 |
| `obj.hh` | 画像高さの半分 |
| `obj.cx` | 画像幅の半分（`hw` の別名） |
| `obj.cy` | 画像高さの半分（`hh` の別名） |
| `obj.cz` | 常に 0 |
| `obj.sz` | 常に 1 |
| `obj.diagonal` | 画像の対角線長（`sqrt(w^2 + h^2)`） |
| `obj.track0`〜`obj.track3` | スライダー値 |
| `obj.time` | アイテム内の経過時間（秒） |
| `obj.frame` | アイテム内の経過フレーム数 |
| `obj.totalframe` | アイテムの総フレーム数 |
| `obj.totaltime` | アイテムの総時間（秒） |
| `obj.t` | `frame / totalframe`（0 除算時は 0） |
| `obj.framerate` | フレームレート |
| `obj.layer` | レイヤー番号 |
| `obj.index` | グループ内のインデックス |
| `obj.num` | グループ内の総数 |

#### 読み書き可能メンバー

| メンバー | 説明 |
|---|---|
| `obj.x`・`obj.y`・`obj.z` | 描画位置（ピクセル） |
| `obj.ox`・`obj.oy`・`obj.oz` | 回転中心のオフセット（ピクセル） |
| `obj.sx`・`obj.sy` | X・Y 方向の拡大率 |
| `obj.zoom` | 拡大率（sx と sy の平均） |
| `obj.aspect` | アスペクト比 |
| `obj.alpha` | 不透明度（0〜255） |
| `obj.rx`・`obj.ry`・`obj.rz` | X・Y・Z 軸回転角（度） |
| `obj.rxr`・`obj.ryr`・`obj.rzr` | X・Y・Z 軸回転角（ラジアン）。変更すると対応する `rx`・`ry`・`rz` を上書きします。 |

#### ピクセル操作関数

| 関数 | 戻り値 | 説明 |
|---|---|---|
| `obj.getpixel(x, y)` | `r, g, b, a` | 指定座標のピクセルを取得します。各成分は 0〜255 の浮動小数点数です。 |
| `obj.setpixel(x, y, r, g, b [, a])` | なし | 指定座標へピクセルを書き込みます。`a` のデフォルトは 255 です。 |
| `obj.getpixeldata()` | `PixelDataProxy` | ピクセルデータへの直接アクセスオブジェクトを返却します。 |
| `obj.putpixeldata()` | なし | 互換性のために存在しますが、何もしません。 |

---

### 6. スクリプト API（scene テーブル）

| メンバー | 説明 |
|---|---|
| `scene.width` | シーン幅（ピクセル） |
| `scene.height` | シーン高さ（ピクセル） |
| `scene.cx` | シーン幅の半分 |
| `scene.cy` | シーン高さの半分 |

---

### 7. スクリプト API（anim テーブル）

`anim` テーブルはアニメーション補助関数と数学ユーティリティを提供します。

#### 定数

| 定数名 | 値 | 説明 |
|---|---|---|
| `anim.tau` | `2π` | 円周率の 2 倍 |
| `anim.e` | `e` | 自然対数の底 |
| `anim.phi` | `(1 + √5) / 2` | 黄金比 |
| `anim.sqrt2` | `√2` | 2 の平方根 |

#### 補間・マッピング関数

| 関数 | 説明 |
|---|---|
| `anim.lerp(a, b, t)` | `a` から `b` への線形補間 |
| `anim.smoothstep(edge0, edge1, x)` | Hermite 補間（0〜1 にクランプ） |
| `anim.smootherstep(edge0, edge1, x)` | 5 次 Hermite 補間（0〜1 にクランプ） |
| `anim.clamp(v, lo, hi)` | `v` を `lo`〜`hi` にクランプ |
| `anim.map(v, a1, b1, a2, b2)` | `a1`〜`b1` の範囲を `a2`〜`b2` へ線形写像 |
| `anim.norm(v, lo, hi)` | `lo`〜`hi` を 0〜1 に正規化 |
| `anim.wrap(v, lo, hi)` | `lo`〜`hi` の範囲でラップアラウンド |
| `anim.pingpong(t, length)` | `0`〜`length` を往復 |

#### ウェーブ・信号関数

| 関数 | 説明 |
|---|---|
| `anim.oscillate(t, lo, hi, freq)` | サイン波で `lo`〜`hi` を周期 `1/freq` 秒で往復 |
| `anim.triangle(t, freq)` | 三角波（0〜1） |
| `anim.square(t, freq)` | 矩形波（0 または 1） |
| `anim.sign(v)` | 符号（−1・0・+1） |
| `anim.step(edge, x)` | `x >= edge` なら 1、それ以外は 0 |
| `anim.fract(v)` | 小数部（`v - floor(v)`） |
| `anim.delay(t, d)` | `max(0, t - d)` |
| `anim.duration(t, dur)` | `clamp(t / dur, 0, 1)` |

#### イージング関数

| 関数 | 説明 |
|---|---|
| `anim.ease_in(t)` | 2 次イーズイン（`t^2`） |
| `anim.ease_out(t)` | 2 次イーズアウト（`1 - (1-t)^2`） |
| `anim.ease_in_out(t)` | 2 次イーズインアウト |
| `anim.elastic(t)` | 弾性イーズアウト（`t` は 0〜1 にクランプ） |
| `anim.back(t)` | バックイーズアウト（定数 c1 = 1.70158） |
| `anim.bounce(t)` | バウンスイーズアウト（`t` は 0〜1 にクランプ） |
| `anim.bezier(t, p0, p1, p2, p3)` | 3 次ベジェ補間（`t` は 0〜1 にクランプ） |

#### 色変換関数

| 関数 | 戻り値 | 説明 |
|---|---|---|
| `anim.hsv_to_rgb(h, s, v)` | `r, g, b` | HSV を RGB（0〜255）に変換。色相 `h` は 0〜360。 |
| `anim.rgb_to_hsv(r, g, b)` | `h, s, v` | RGB（0〜255）を HSV に変換。色相 `h` は 0〜360。 |

#### 2D ベクトル演算関数

| 関数 | 戻り値 | 説明 |
|---|---|---|
| `anim.len(x, y)` | `double` | ベクトルの長さ |
| `anim.dist(x1, y1, x2, y2)` | `double` | 2 点間の距離 |
| `anim.dot(x1, y1, x2, y2)` | `double` | 内積 |
| `anim.normalize(x, y)` | `x, y` | 正規化。長さが 0 の場合は `(0, 0)` を返却します。 |
| `anim.polar(r, a)` | `x, y` | 極座標（半径 `r`・角度 `a` 度）を直交座標へ変換 |
| `anim.rotate(x, y, a)` | `x, y` | ベクトルを `a` 度回転 |

#### ノイズ・乱数関数

| 関数 | 戻り値 | 説明 |
|---|---|---|
| `anim.noise(x [, y [, z]])` | `double` | 値域 0〜1 の 1〜3 次元スムースノイズ（格子点ハッシュによる補間） |
| `anim.rand([max, ] seed)` | `double` | シードベースの決定論的乱数。引数 1 個でシードのみ、2 個で `[0, max]`、3 個で `[min, max]` |

---

### 8. スクリプト API（ymm4 テーブル）

`ymm4` テーブルは YMM4 固有の情報を提供します。

| メンバー | 型 | 説明 |
|---|---|---|
| `ymm4.group_index` | `int` | エフェクトグループ内のインデックス |
| `ymm4.group_count` | `int` | エフェクトグループの総数 |
| `ymm4.group_ratio` | `double` | `group_index / group_count`（0 除算時は 0） |
| `ymm4.timeline_totalframe` | `int` | タイムライン全体の総フレーム数 |
| `ymm4.timeline_totaltime` | `double` | タイムライン全体の総時間（秒） |
| `ymm4.is_saving` | `bool` | エクスポート中かどうか |
| `ymm4.time_ratio` | `double` | アイテム内の時間比率（Frame / TotalFrame） |
| `ymm4.is_playing` | `bool` | 再生中かどうか |
| `ymm4.is_paused` | `bool` | 一時停止中かどうか |
| `ymm4.scene_id` | `string` | シーン ID（GUID 文字列） |

---

### 9. ピクセルデータプロキシ（PixelDataProxy）

`PixelDataProxy` は `[MoonSharpUserData]` 属性を持つ内部クラスで、
`obj.getpixeldata()` の戻り値として Lua から直接操作できます。

- `width`・`height`：画像の幅と高さを返却する読み取り専用メンバーです。
- `get(index)`：1 始まりのフラットインデックスでチャンネルデータを取得します。
  チャンネル順は `R, G, B, A` の 4 チャンネルで 1 ピクセルを構成し、
  `R`・`G`・`B` はプリマルチプライ済みからストレートアルファに復元した 0〜255 の値、
  `A` は 0〜255 のアルファ値を返却します。
- `set(index, value)`：1 始まりのフラットインデックスでチャンネルデータを書き込みます。
  `R`・`G`・`B` チャンネルはストレートアルファの値をプリマルチプライ済みに変換して格納します。
  `A` チャンネルを変更すると、既存の RGB 成分を新旧アルファ比でスケーリングして整合させます。

---

### 10. コードエディター拡張

#### LuaFoldingStrategy

AvalonEdit 向けの折り畳み戦略を実装します。
`function`・`do`・`if` / `end` ブロックと `repeat` / `until` ブロックを対象に
複数行にまたがるブロックの折り畳み領域を生成します。
複数行コメント（`--[[ ... ]]`）・文字列リテラル・単行コメントを正規表現でトークン化し、
ブロック開閉を誤認しないよう除外します。

#### LuaAutoCompletionStrategy

AvalonEdit 向けのオートコンプリート戦略を実装します。

提供する補完候補は以下のとおりです。

- Lua キーワード（`and`・`break`・`do`・`else`・`elseif`・`end`・`false`・`for`・
  `function`・`goto`・`if`・`in`・`local`・`nil`・`not`・`or`・`repeat`・`return`・
  `then`・`true`・`until`・`while`）
- グローバル変数（`time`・`frame`・`totalframe`・`framerate`・`timelineframe`・
  `timelinetime`・`layer`・`obj`・`scene`・`anim`・`ymm4`・`math`・`string`・`table`・
  `bit32`・`type`・`tostring`・`tonumber`・`select`・`error`・`assert`・`print`・
  `ipairs`・`pairs`・`next`・`unpack`・`setmetatable`・`getmetatable`・`rawget`・
  `rawset`・`rawequal`・`rawlen`・`pcall`・`xpcall`）
- 各名前空間のメンバー（ドット入力後に絞り込み）
- ソースコード内のユーザー定義関数（`function foo()`・`local function foo()` のパターンを抽出）

---

### 11. ツールバー（LuaScriptToolBar）

コードエディター上部に表示される WPF `UserControl` です。

- インポートボタン：ファイルダイアログで `.lua` ファイルを選択し、
  UTF-8 として読み込んでスクリプトに設定します。
- エクスポートボタン：現在のスクリプトを UTF-8 BOM なしで `.lua` ファイルに保存します。
  複数選択時は最初のアイテムのスクリプトを保存します。
- クリアボタン：確認ダイアログ後にスクリプトを `DefaultScript` へリセットします。

ファイルアクセスエラーは `IOException`・`UnauthorizedAccessException` を捕捉して
メッセージボックスで通知します。

---

### 12. 例外定義

| クラス | 基底 | 説明 |
|---|---|---|
| `LuaScriptException` | `Exception` | Lua スクリプト例外の基底クラス（内部抽象クラス） |
| `LuaScriptCompilationException` | `LuaScriptException` | 構文エラー時にスローされます。 |
| `LuaScriptRuntimeException` | `LuaScriptException` | 実行時エラーまたはタイムアウト時にスローされます。 |

---

### 13. ローカライズ（Texts）

`Texts` クラスは `[AutoGenLocalizer]` 属性を持つ `partial` クラスとして宣言されます。
`YukkuriMovieMaker.Generator` のソースジェネレーターが `Texts.csv` を処理し、
各ロケールのリソースファイルを自動生成します。

対応言語：日本語 (`ja-jp`)・英語 (`en-us`)・中国語簡体字 (`zh-cn`)・中国語繁体字 (`zh-tw`)・
韓国語 (`ko-kr`)・スペイン語 (`es-es`)・アラビア語 (`ar-sa`)・インドネシア語 (`id-id`)

ローカライズキーの一覧は以下のとおりです。

| キー | ja-jp |
|---|---|
| `LuaScript` | Lua スクリプト |
| `ScriptGroup` | スクリプト |
| `ScriptCode` | スクリプトコード |
| `ScriptCodeDesc` | Lua スクリプトを記述します。obj.x/y/zoom/alpha 等でエフェクトを制御できます。 |
| `ParametersGroup` | パラメータ |
| `Track0` | トラック 0 |
| `Track1` | トラック 1 |
| `Track2` | トラック 2 |
| `Track3` | トラック 3 |
| `TrackDesc` | スライダー値（AviUtl 準拠 track0〜3 に対応） |
| `ToolBarImport` | インポート |
| `ToolBarExport` | エクスポート |
| `ToolBarClear` | クリア |
| `ToolBarClearConfirm` | スクリプトをデフォルトに戻しますか？ |
| `ToolBarClearTitle` | 確認 |
| `ToolBarImportErrorTitle` | インポートエラー |
| `ToolBarExportErrorTitle` | エクスポートエラー |
| `LuaFileFilter` | Lua スクリプト (*.lua) \| *.lua \| すべてのファイル (*.*) \| *.* |
