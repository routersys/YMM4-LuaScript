# v1.11.0 - Lua スクリプト for YMM4

AviUtl 互換の描画 API を拡張したリリースです。`obj.draw` / `obj.drawpoly` の合成モード、
作業画像の出力可否を切り替える `draw_state`、描画先を `tmp` バッファへ切り替える
`drawtarget`、アンカー座標を Lua 変数として扱う `obj.setanchor` を追加しました。
MoonSharp とネイティブの両エンジンで同じ API を使えるようにし、内蔵ドキュメントも
各言語で更新しました。

---

## 新機能・機能改善

### 1. obj.setoption("blend", 合成モード)

`obj.draw` / `obj.drawpoly` の合成モードを `obj.setoption("blend", 番号)` で指定できるように
しました。

- 番号は YMM4 のプロパティパネルにある「合成モード」の並びと同じで、0 から始まります。
- 0 は通常、1 はディザ合成、2 は比較（暗）、3 は乗算、4 は焼き込みカラー、5 は
  焼き込み（リニア）、6 は比較（明）、7 はスクリーン、8 は覆い焼きカラー、9 は
  覆い焼き（リニア） - 加算、10 は加算、11 はオーバーレイ、12 はソフトライト、13 は
  ハードライト、14 はビビッドライト、15 はリニアライト、16 はピンライト、17 は
  ハードミックス、18 は差分、19 は除外、20 は減算、21 は除算、22 は色相、23 は彩度、
  24 はカラー、25 は輝度、26 はカラー比較（明）、27 は背景、28 はカラー比較（暗）、
  29 は削除、30 は背景でクリッピング、31 は重ならない部分のみ、32 は色反転マスクです。
- 範囲外の値や数値ではない値は通常として扱います。
- MoonSharp とネイティブの両エンジンで、`obj.draw` と `obj.drawpoly` の描画コマンドへ
  合成モードを渡します。

### 2. obj.setoption("draw_state", true / false)

このエフェクトが作業画像を出力するかどうかを、スクリプトから明示できるようにしました。

- `true` を指定すると、`setpixel` や `obj.draw` を使わない場合でも作業画像を出力します。
- `false` を指定すると、このエフェクトの作業画像出力を抑制します。
- 未指定の場合は従来どおり、ピクセル変更や描画コマンドの有無に従います。
- MoonSharp とネイティブの両エンジンで動作します。

### 3. obj.setoption("drawtarget", "framebuffer" / "tempbuffer")

`obj.draw` / `obj.drawpoly` の描画先を切り替えられるようにしました。

- `"framebuffer"` は既定の描画先です。描画コマンドはフレームバッファへ合成されます。
- `"tempbuffer"` を指定すると、描画コマンドは作業用の `tmp` バッファへその場で合成されます。
- `obj.setoption("drawtarget", "tempbuffer", w, h)` のように幅と高さを渡すと、そのサイズで
  `tmp` バッファを用意します。
- `tmp` へ描いた結果は `obj.copybuffer("obj", "tmp")` で作業画像へ戻せます。
- `tempbuffer` への合成は通常のソースオーバーで行い、`blend` はフレームバッファへの描画に
  適用します。
- 描画先は毎フレーム既定の `"framebuffer"` へ戻ります。
- MoonSharp とネイティブの両エンジンで動作します。

### 4. obj.setanchor(name, count [, option, ...])

アンカー座標を Lua 変数へ格納する `obj.setanchor` を追加しました。

- `name` で指定した名前の Lua 変数へ、`{x0, y0, x1, y1, ...}` の配列を作成します。
- `"xyz"` を指定した場合は `{x0, y0, z0, x1, y1, z1, ...}` の 3D 座標になります。
- `count` は 0〜32 に制限され、戻り値は実際に設置した個数です。
- 保存済みの座標がある場合はその値を使い、無い場合は 8 列の既定グリッド座標を使います。
- `"line"` / `"loop"` / `"star"` / `"arm"` の線結び指定と、`"xyz"` の 3D 指定を受け付けます。
- `name` に `"track"` を指定するトラックバー連携は未対応で、0 を返します。
- MoonSharp とネイティブの両エンジンで同じ座標テーブルを受け取れるようにしました。

### 5. 内蔵ドキュメントの更新

同梱される `LuaScript/docs/LuaScript.*.txt` を更新し、`blend`、`draw_state`、
`drawtarget`、`obj.setanchor` の説明を各言語へ追加しました。

---

## 互換性・後方互換

- `blend` の既定値は 0 の通常です。指定しない既存スクリプトの合成結果は変わりません。
- `drawtarget` の既定値は `"framebuffer"` です。フレームをまたいで前回の描画先は持ち越しません。
- `draw_state` を指定しない場合は、従来どおりピクセル変更や描画コマンドに応じて出力します。
- `obj.setanchor` を含むスクリプトは、自動判定では描画 API を使うスクリプトとして扱います。
  ネイティブで実行する場合は、従来どおり明示ディレクティブを使えます。
- `tempbuffer` への描画は通常合成です。`blend` による合成モード指定は、フレームバッファへの
  `obj.draw` / `obj.drawpoly` に適用されます。
- `obj.setanchor("track", ...)` によるトラックバー連携は未対応で、0 を返します。

---

## 内部実装

- YMM4 の `Blend` 列挙値へ番号を対応させる `BlendModeMap` を追加し、`DrawCommand` へ
  合成モード番号を持たせました。
- `DrawCompositor` は通常以外の合成モードで `DrawImage` / `BlendImage` を使い、Opacity の
  出力をキャッシュして毎描画ごとの COM オブジェクト確保を避けるようにしました。
- `SoftwareCompositor` を追加し、MoonSharp 側で最近傍または線形補間による
  `obj.draw` / `obj.drawpoly` の `tempbuffer` 合成を行えるようにしました。
- ネイティブワーカー側にも同等のソフトウェア合成処理を追加しました。
- `AviUtlScriptContext` に `DrawStateOverride`、描画先状態、アンカー入力元、アンカー要求一覧を
  追加しました。描画先が `tempbuffer` の場合は `DrawCommand` を通常の描画キューへ積まず、
  その場で `tmp` バッファへ合成します。
- `LuaAnchorPoint` と `AnchorSupport` を追加し、アンカー数の制限、オプション解析、既定座標、
  保存済み座標の解決、ドラッグ後の座標更新を分離しました。
- アンカー要求から生成した `VideoEffectController` を `DrawDescription.Controllers` へ追加し、
  YMM4 のプレビュー操作へ渡すようにしました。
- アンカー座標の変更をキャッシュキーへ含めるため、エフェクト側へ `AnchorVersion` を追加しました。
- ネイティブプロトコルへ `draw_state` の書き戻しフィールドと `setanchor` コールバック種別を
  追加しました。`LuaJitWorker` は `draw` / `drawpoly` の合成モード、`draw_state`、
  `setanchor` の座標解決をワーカーとやり取りします。
- ネイティブワーカーでは `drawtarget` 用の `tmp` バッファ、`obj.setanchor`、
  `draw_state` の書き戻しを実装しました。
- ネイティブワーカーの `obj.copybuffer` は、`tmp` や名前付きキャッシュへ書き込むときに
  バッファを複製するようにしました。
- `ScriptDirective` は `obj.setanchor` を描画 API として検出します。
- `NativeFieldMap` から未使用の `Epsilon` 定数を削除しました。
- `AnchorSupportTests` と `SoftwareCompositorTests` を追加し、ネイティブ往復テストへ
  `blend`、`draw_state`、`setanchor`、`drawtarget` の検証を追加しました。
