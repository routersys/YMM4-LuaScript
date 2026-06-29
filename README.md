# Luaスクリプト for YMM4
 
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/YMM4-LuaScript.svg)](https://github.com/routersys/YMM4-LuaScript/releases)
 
---
 
YukkuriMovieMaker4（YMM4）上で動作する、**Luaスクリプトでオブジェクトの座標・拡縮・回転・不透明度・ピクセルを直接制御できる映像エフェクトプラグイン**です。
スクリプトエンジンには MoonSharp（Lua 5.2 互換）が採用されており、`obj.x` や `obj.alpha` といった変数をスクリプトから書き換えることで、AviUtl のスクリプト制御に近い感覚でアニメーションを組み立てられます。
 
座標・拡縮・回転・不透明度の制御に加えて、`obj.getpixel` / `obj.setpixel` / `obj.getpixeldata` によるピクセル単位の画像加工、アニメーション制作向けの数学ユーティリティ群（`anim` テーブル）、4 系統のトラックバー（Track0〜Track3）によるスクリプトへのパラメータ受け渡しに対応しています。
 
![Image](https://github.com/routersys/YMM4-LuaScript/blob/main/docs/LuaScript.png)
 
---
 
## 目次
 
1. [概要](#概要)
2. [動作要件](#動作要件)
3. [インストール方法](#インストール方法)
4. [主な機能](#主な機能)
   - [1. スクリプトエンジン](#1-スクリプトエンジン)
   - [2. obj テーブルによる描画制御](#2-obj-テーブルによる描画制御)
   - [3. ピクセル操作機能](#3-ピクセル操作機能)
   - [4. scene テーブルと ymm4 テーブル](#4-scene-テーブルと-ymm4-テーブル)
   - [5. anim ユーティリティテーブル](#5-anim-ユーティリティテーブル)
   - [6. トラックバー（Track0〜Track3）](#6-トラックバーtrack0track3)
   - [7. スクリプトエディタの支援機能](#7-スクリプトエディタの支援機能)
   - [8. インポート・エクスポート・クリア](#8-インポートエクスポートクリア)
   - [9. AviUtl スクリプトの部分互換](#9-aviutl-スクリプトの部分互換)
5. [実行モデル](#実行モデル)
   - [実行エンジンの自動振り分け](#実行エンジンの自動振り分け)
   - [実行スレッドとタイムアウト](#実行スレッドとタイムアウト)
   - [実行結果のキャッシュ](#実行結果のキャッシュ)
   - [グローバル変数のリセットと整合性チェック](#グローバル変数のリセットと整合性チェック)
6. [パラメータ一覧](#パラメータ一覧)
7. [使い方ガイド](#使い方ガイド)
8. [注意事項](#注意事項)
9. [免責事項](#免責事項)
10. [ライセンス](#ライセンス)
---
 
## 概要
 
本プラグインは YMM4 の「映像エフェクト」として動作し、エフェクトの種類一覧では「Luaスクリプト」として表示されます。
 
内部的には MoonSharp（Lua 5.2 互換のスクリプトエンジン）を組み込んでおり、ユーザーが記述した Lua スクリプトをフレームごとに実行します。スクリプト内で `obj.x` や `obj.alpha` といったグローバル変数を書き換えると、その値がオブジェクトの座標・拡縮・回転・不透明度として描画に反映されます。
 
このエフェクトは独自のスクリプト実行系を基盤としつつ、AviUtl のアニメーション効果スクリプト（`.anm` 系）の一部に対する互換層を備えています（詳細は[AviUtl スクリプトの部分互換](#9-aviutl-スクリプトの部分互換)を参照）。AviUtl 向けの EXO 出力（`.exo`）には対応していません。
 
---
 
## 動作要件
 
| 項目 | 要件 |
|---|---|
| OS | Windows 10 バージョン 2004（ビルド 19041）以降 / Windows 11（64bit） |
| YukkuriMovieMaker4 | 最新版を推奨 |
| ランタイム | .NET 10.0 |
 
※ スクリプトエンジン（MoonSharp）はプラグインに同梱されており、別途インストールする必要はありません。
 
---
 
## インストール方法
 
1. [Releases](https://github.com/routersys/YMM4-LuaScript/releases/latest) ページから最新のプラグインファイル（`.ymme`）をダウンロードしてください。
2. YMM4 が起動していないことを確認し、ダウンロードしたファイルを実行（ダブルクリック）してインストールします。
3. YMM4 を起動し、タイムライン上のアイテムに映像エフェクトを追加します。
4. 映像エフェクトの種類として **「Luaスクリプト」** を選択してください。
---
 
## 主な機能
 
### 1. スクリプトエンジン
 
スクリプトの実行エンジンには MoonSharp（Lua 5.2 互換）を使用しています。加えて、自ピクセル完結のピクセル写像（`obj.getpixel` で読み取り `obj.setpixel` で書き戻す二重ループ）は構文解析で検出して **CPU カーネル（式ツリー）** または **GPU カーネル（Direct2D ピクセルシェーダ）** へ実行時コンパイルして高速化し、それ以外のピクセル操作を含むスクリプトは同梱の高速ランタイム（**LuaJIT**）で自動実行されます（詳細は[実行エンジンの自動振り分け](#実行エンジンの自動振り分け)を参照）。いずれのエンジンでも利用可能な標準ライブラリは以下に限定されています。
 
- `table`、`string`、`math`、`bit32`
- 基本関数: `type`、`tostring`、`tonumber`、`select`、`error`、`assert`、`print`、`ipairs`、`pairs`、`next`、`unpack`、`setmetatable`、`getmetatable`、`rawget`、`rawset`、`rawequal`、`rawlen`、`pcall`、`xpcall`
`io`、`os`、`debug`、`ffi`、`require` 等は利用できません。ファイルへのアクセス（`require` を含む）はスクリプトローダー側で明示的に禁止されており、呼び出すと例外になります。
 
### 2. obj テーブルによる描画制御
 
`obj` テーブルにはオブジェクトの現在の描画情報が格納されており、一部の変数を書き換えるとスクリプト実行後にその値が描画へ反映されます。
 
| 変数 | 変更可否 | 説明 |
|---|---|---|
| `obj.x`, `obj.y`, `obj.z` | 変更可 | 表示基準座標。描画位置（Draw.X/Y/Z）に反映される |
| `obj.ox`, `obj.oy` | 変更可 | 中心点オフセット。描画の中心位置（CenterPoint.X/Y）に反映される |
| `obj.oz` | 変更可（出力には未使用） | 値はスクリプト実行後に読み戻されるが、現状の出力計算では使用されない |
| `obj.sx`, `obj.sy` | 変更可 | X/Y 方向の独立した拡大率。`obj.zoom` / `obj.aspect` の変更より優先される |
| `obj.zoom`, `obj.aspect` | 変更可 | `obj.sx` / `obj.sy` が同時に変更されていない場合のみ、`zoom × (1 ± aspect)` として sx/sy に反映される |
| `obj.alpha` | 変更可 | 不透明度（0.0〜255.0）。出力時に 255 で除算され、0.0〜1.0 の Opacity に変換される |
| `obj.rx`, `obj.ry`, `obj.rz` | 変更可 | 回転角（度）。対応する `obj.rxr` / `obj.ryr` / `obj.rzr` が同時に変更されている場合はそちらが優先される |
| `obj.rxr`, `obj.ryr`, `obj.rzr` | 変更可 | 回転角（ラジアン）。同名の度数版より優先して反映される |
| `obj.w`, `obj.h`, `obj.hw`, `obj.hh`, `obj.cx`, `obj.cy`, `obj.cz`, `obj.diagonal` | 読み取り専用 | 入力画像のサイズ・中心座標・対角線長 |
| `obj.track0`〜`obj.track3` | 読み取り専用 | トラックバーの現在値（後述） |
| `obj.time`, `obj.frame`, `obj.totalframe`, `obj.totaltime`, `obj.t`, `obj.framerate`, `obj.layer`, `obj.index`, `obj.num` | 読み取り専用 | 時間・フレーム・レイヤー等の情報 |
 
### 3. ピクセル操作機能
 
`obj.getpixel` / `obj.setpixel` / `obj.getpixeldata` を使うと、入力画像のピクセルを直接読み書きできます。
 
- **`obj.getpixel(x, y)`**: 指定座標のピクセルを `r, g, b, a`（各 0.0〜255.0、ストレートアルファ相当）の多値返却で取得します。範囲外の座標を指定した場合は `(0, 0, 0, 0)` を返します。
- **`obj.setpixel(x, y, r, g, b [, a])`**: 指定座標のピクセルを書き換えます。`a` 省略時は 255 として扱われます。内部ではプリマルチプライドアルファ（BGRA）形式で保持され、書き込み時に変換されます。範囲外の座標を指定した場合は何も行いません。
- **`obj.getpixeldata()`**: 全ピクセルへ一括アクセスするためのプロキシオブジェクトを返します。`.width` / `.height` に加えて、`:get(index)` / `:set(index, value)` を持ちます。インデックスは 1 始まりで、R, G, B, A の順に並びます（`(y * obj.w + x) * 4 + 1` が R チャンネル）。アルファチャンネルを変更すると RGB が比率変換され、RGB チャンネルを変更すると内部的にプリマルチプライド変換が行われます。
- **`obj.putpixeldata()`**: 互換性のために定義された空実装関数です。`setpixel` / `PixelDataProxy:set` による変更は自動的に反映されるため、呼び出す必要はありません。
`getpixel` / `setpixel` / `getpixeldata` のいずれかを最初に呼び出した時点で、GPU 上の画像データを CPU 側へ転送する処理が一度だけ発生します。ピクセル操作を行わないスクリプトではこの転送は発生しません。
 
これらのピクセル操作を含むスクリプトは自動的に高速化されます。自ピクセル完結のピクセル写像は CPU カーネル（式ツリー）または GPU カーネル（Direct2D ピクセルシェーダ）へ、それ以外は高速ランタイム（LuaJIT）へ自動で振り分けられ、いずれも従来エンジンに比べて大幅（内部計測で数十倍）に高速化されます。CPU カーネルと LuaJIT の出力結果は従来エンジンと一致します（GPU カーネルのみ float32 精度のためごく僅かに異なり得ます）。詳細・制約は[実行エンジンの自動振り分け](#実行エンジンの自動振り分け)を参照してください。
 
### 4. scene テーブルと ymm4 テーブル
 
`scene` テーブルにはシーン（プロジェクト）の解像度情報が格納されています。いずれも読み取り専用です。
 
| 変数 | 説明 |
|---|---|
| `scene.width`, `scene.height` | シーンの横・縦解像度（ピクセル） |
| `scene.cx`, `scene.cy` | シーン解像度の半分（width/2, height/2） |
 
`ymm4` テーブルには YMM4 固有のメタ情報が格納されています。いずれも読み取り専用です。
 
| 変数 | 説明 |
|---|---|
| `ymm4.group_index`, `ymm4.group_count` | グループ制御内の現在のインデックスと総アイテム数 |
| `ymm4.group_ratio` | グループ内の進行割合（group_index / group_count） |
| `ymm4.timeline_totalframe`, `ymm4.timeline_totaltime` | タイムライン全体の総フレーム数・総時間 |
| `ymm4.time_ratio` | 現在の再生位置の割合（0.0〜1.0） |
| `ymm4.is_saving` | 動画出力中かどうか |
| `ymm4.is_playing`, `ymm4.is_paused` | 再生中・一時停止中かどうか |
| `ymm4.scene_id` | 現在のシーンを一意に識別する文字列 |
 
### 5. anim ユーティリティテーブル
 
`anim` テーブルには、アニメーション計算に使う純粋な定数・関数群が登録されています。コンテキストに依存しない純粋関数であり、副作用はありません。
 
| 関数 / 定数 | 説明 |
|---|---|
| `anim.tau`, `anim.e`, `anim.phi`, `anim.sqrt2` | 円周率の2倍、ネイピア数、黄金比、2の平方根 |
| `anim.lerp(a, b, t)` | a と b を t（0.0〜1.0）で線形補間 |
| `anim.smoothstep(e0, e1, x)` | e0〜e1 の間をエルミート補間で遷移（3次） |
| `anim.smootherstep(e0, e1, x)` | e0〜e1 の間をより滑らかに遷移（5次、2次導関数まで連続） |
| `anim.clamp(v, lo, hi)` | v を lo〜hi の範囲に収める |
| `anim.map(v, a1, b1, a2, b2)` | a1〜b1 の範囲の v を a2〜b2 の範囲に変換 |
| `anim.norm(v, lo, hi)` | v が lo〜hi の範囲でどの割合（0.0〜1.0）かを取得 |
| `anim.wrap(v, lo, hi)` | v を lo〜hi の範囲でループさせる |
| `anim.pingpong(t, length)` | 0〜length の範囲を往復する値 |
| `anim.sign(v)` | v の符号（正=1、負=-1、ゼロ=0） |
| `anim.oscillate(t, lo, hi, freq)` | freq の周波数で lo〜hi を正弦波で往復 |
| `anim.triangle(t, freq)` | freq の周波数で 0.0〜1.0 を往復する三角波 |
| `anim.square(t, freq)` | freq の周波数で 0.0 と 1.0 を切り替える矩形波 |
| `anim.duration(t, dur)` | dur に対する t の割合（0.0〜1.0、クランプあり） |
| `anim.delay(t, d)` | d 経過後から始まる時間（0 以下は 0） |
| `anim.ease_in(t)` / `anim.ease_out(t)` / `anim.ease_in_out(t)` | 2次のイーズイン／イーズアウト／イーズインアウト |
| `anim.elastic(t)` | ばねのように振動しながら収束するイージング（t は 0.0〜1.0 にクランプ） |
| `anim.back(t)` | 行き過ぎてから戻るイージング |
| `anim.step(edge, x)` | x が edge 以上なら 1.0、未満なら 0.0 |
| `anim.fract(v)` | v の小数部分のみを取得 |
| `anim.bounce(t)` | バウンドするようなイージング（t は 0.0〜1.0 にクランプ） |
| `anim.hsv_to_rgb(h, s, v)` | HSV（色相0〜360、彩度0〜1、明度0〜1）を RGB（各0〜255）に変換 |
| `anim.rgb_to_hsv(r, g, b)` | RGB（各0〜255）を HSV に変換 |
| `anim.len(x, y)` | ベクトル (x, y) の長さ |
| `anim.dist(x1, y1, x2, y2)` | 2点間の距離 |
| `anim.dot(x1, y1, x2, y2)` | 2つのベクトルの内積 |
| `anim.normalize(x, y)` | ベクトル (x, y) を正規化した方向ベクトル（長さ0の場合は (0, 0)） |
| `anim.polar(r, a)` | 半径 r、角度 a（度）から (x, y) を取得 |
| `anim.rotate(x, y, a)` | 座標 (x, y) を a（度）回転させた座標を取得 |
| `anim.bezier(t, p0, p1, p2, p3)` | t（0.0〜1.0）における3次ベジェ曲線の値 |
| `anim.rand(min, max, seed)` | seed に基づく決定論的な乱数（min/max 省略時は 0.0〜1.0） |
| `anim.noise(x, y, z)` | 1〜3次元の滑らかな値ノイズ。戻り値の範囲は **0.0〜1.0** |
 
### 6. トラックバー（Track0〜Track3）
 
エフェクトパネルには 4 本のトラックバー（アニメーション対応）があり、各値はスクリプト内で `obj.track0`〜`obj.track3` として参照できます。スライダー表示範囲はいずれも -100〜100、デフォルト値は 0、表示形式は小数点以下2桁です。トラックバーは常に表示されます。

チェックボックス（`obj.check0`〜`obj.check3`）とカラーピッカー（`color`）は、スクリプト本体でそれらを参照すると自動的にエフェクトパネルへ表示されます（参照を消すと非表示に戻ります）。これにより、`--` ヘッダを書かなくても必要な入力欄だけを出せます。なお、この一般利用ではスライダーのようなラベル・範囲・既定値の宣言は伴わないため、値の範囲はスクリプト内で扱ってください。AviUtl 互換の `--track`/`--check`/`--color` ヘッダによる宣言表示は従来どおり利用できます（[AviUtl スクリプトの部分互換](#9-aviutl-スクリプトの部分互換)を参照）。
 
### 7. スクリプトエディタの支援機能
 
- **構文ハイライト**: Lua 用のシンタックス定義（ダーク/ライトテーマ）が同梱されており、コメント・文字列・数値リテラル・予約語・`true`/`false`・`nil` を色分け表示します。ブロックコメントは `--[[ ]]` 形式のみ認識し、`--[==[ ]==]` のようなレベル付き長括弧には対応していません。
- **コード補完**: Lua の予約語、グローバル変数、および `obj` / `math` / `string` / `table` / `bit32` / `scene` / `anim` / `ymm4` の各テーブルのメンバーを補完候補として提示します。加えて、編集中のスクリプト内で `function 名前(...)` または `local function 名前(...)` の形式で定義された関数名も動的に検出し、補完候補に追加します。
- **コード折り畳み**: `function` / `do` / `if` 〜 `end`、および `repeat` 〜 `until` のブロックを折り畳めます。コメントや文字列リテラル内に現れる予約語は誤って折り畳み対象にならないよう除外されます（こちらはレベル付き長括弧の文字列・コメントにも対応しています）。対応の取れていない開始・終了が見つかった場合はエラー位置として検出されます。
### 8. インポート・エクスポート・クリア
 
スクリプトエディタ上部のツールバーから、以下の操作が行えます。
 
- **インポート**: `.lua` ファイルを選択して読み込み、選択中のすべてのアイテムのスクリプトを置き換えます。
- **エクスポート**: 現在のスクリプトを `.lua` ファイルとして保存します。複数のアイテムを選択している場合でも、書き出されるのは先頭の 1 件のスクリプトのみです。
- **クリア**: 確認ダイアログの表示後、選択中のすべてのアイテムのスクリプトをデフォルトスクリプト（`obj.alpha = math.min(time * 255, 255)`）に戻します。

### 9. AviUtl スクリプトの部分互換

AviUtl のアニメーション効果スクリプト（`.anm` 系）のうち、座標・拡縮・回転・不透明度の変形、乱数、および図形描画について、追加の記述変更なしに実行できる互換層を備えています。スクリプトに AviUtl 固有の記法が含まれない場合、この互換層は一切介入しません（従来のスクリプトの挙動は変わりません）。

対応している内容は次のとおりです。

- **スクリプトヘッダの解釈**: コメント形式のパラメータ宣言を解釈し、スクリプト本体が参照する変数を実行前に定義します。
  - `--dialog:項目名,local 変数=既定値;…` … 各項目の宣言部（`local 変数=既定値` など）を本体の先頭に展開します。AviUtl では対話ダイアログで編集される値ですが、本プラグインでは既定値で動作します。
  - `--color:0xRRGGBB` … 変数 `color` を指定色で定義します。
  - `--check0:項目名,既定値` … `obj.check0`（`0`〜`3`）を既定値で定義します。
  - `--param:任意のLua文` … 記述した Lua 文をそのまま本体の先頭に展開します。
  - `--track0:`〜`--track3:` … トラックバーの値は従来どおり `obj.track0`〜`obj.track3` から参照します（ラベル・範囲・既定値の UI 反映は行いません）。
- **`@` による複数セクション**: 1 つのスクリプトに `@名前` で複数の効果が定義されている場合、先頭のセクションのみを実行します。
- **`obj.rand(st, ed [, seed [, frame]])`**: 指定範囲（`st`〜`ed`）の整数を返す決定論的な乱数です。`seed` と `frame` が同じであれば常に同じ値を返し、`frame` を省略すると現在のフレームを用います。従来エンジン（MoonSharp）と高速ランタイム（LuaJIT）で同じ値を返します。
- **`obj.load("figure", 名前, 色, サイズ [, ライン [, アスペクト]])`**: 円・四角形・三角形・五角形・六角形・星形を生成し、オブジェクトのバッファを置き換えて描画します（出力は図形サイズに合わせて生成され、中心に配置されます）。`ライン` を省略するか `0` で塗りつぶし、サイズ以上の値でも塗りつぶしになります。`アスペクト`（-1.0〜1.0）で幅・高さを歪曲できます。
- **描画・メディア・バッファ・オプション API**: `obj.draw` / `obj.drawpoly` による描画合成、`obj.load` の `"text"` / `"image"` / `"movie"`、`obj.setfont`、`obj.copybuffer`、`obj.effect`、`obj.getvalue`、`obj.setoption` / `obj.getoption` / `obj.pixeloption` に対応しています。これらは従来エンジン（MoonSharp）と高速ランタイム（LuaJIT）の双方で動作します。

次のような機能は、この互換層では対象外です（呼び出しを含むスクリプトは意図どおりには動作しません）。

- `obj.setanchor`、`scene.set` / `scene.get` などの未実装関数
- `require` によるモジュール読み込み、`io` / `dofile` などの任意ファイルアクセス（`obj.load("image"/"movie")` のみ、先頭ヘッダー検証付きで対応）

---
 
## 実行モデル
 
### 実行エンジンの自動振り分け
 
スクリプトは内容に応じて 4 つのエンジンへ自動的に振り分けられます。
 
- **自ピクセル完結のピクセル写像**（`for y … for x … obj.getpixel … obj.setpixel … end end` の形）→ **CPU カーネル（式ツリー）** へ実行時コンパイルして実行。
- **上記以外のピクセル操作を含むスクリプト**（`obj.getpixeldata` や近傍参照・別座標書き込みなど）→ 同梱の高速ランタイム **LuaJIT** で実行（ネイティブ実行）。
- **変形のみのスクリプト** → 従来の **MoonSharp** で実行。
 
スクリプトの先頭行に次のディレクティブを書くと、エンジンを明示指定できます。
 
- `--!cpu` … CPU カーネル（式ツリー）で実行。全 CPU コアで並列実行し、出力は MoonSharp とビット単位で一致します。
- `--!gpu` … GPU カーネル（Direct2D ピクセルシェーダ）で実行。GPU→CPU 転送を排して最速ですが、float32 精度のため結果はごく僅かに異なり得ます（オプトイン）。
- `--!native` … 高速ランタイム（LuaJIT）で実行
- `--!moonsharp` … 従来エンジン（MoonSharp）で実行
 
いずれのディレクティブも、利用不可・非対応の場合は出力を壊さずに下位のエンジンへ自動的にフォールバックします（例：`--!gpu` の初期化に失敗した場合は CPU カーネル、さらに MoonSharp へ縮退します）。
 
CPU / GPU カーネルが成立するのは、四則演算・`math.*`・比較・しきい値の「条件 and 値1 or 値2」・`local` 変数のみを使い、処理中の自ピクセル（`obj.getpixel(x, y)`）と `obj` / `scene` / `time` 等の値だけを参照する純粋な写像（色変換・グレースケール・ガンマ・しきい値・チャンネル混合など）の場合です。近傍参照（ぼかし等）・別座標への書き込み・`getpixeldata`・`getobject`・テーブル/文字列操作・`if` 文などを含む場合はカーネル化されず、自動的に LuaJIT または MoonSharp で実行されます。コンパイル済みのカーネルはスクリプト本文をキーにキャッシュされ、初回フレームのみコンパイルコストを払います。
 
ネイティブ実行は**専用の別プロセス**で行われ、ピクセルバッファは共有メモリ経由で受け渡されます。スクリプトが無限ループに陥った場合でも **5 秒のタイムアウトでプロセスを強制終了**するため本体は固まらず、終了後は自動的に復帰します。出力結果は従来エンジンと一致します。
 
`obj.getobject` はネイティブ実行でも利用できます。呼び出し時に本体プロセスへ同期的に問い合わせてシーンオブジェクトを解決するため、従来エンジンと同じ結果を返します。同じタグ・同じフレームへの連続した問い合わせはフレーム内でキャッシュされ、2 回目以降は往復を行いません。

ネイティブ実行には次の制約があります。
 
- `bit32` は正しい Lua 5.2 の挙動になります。実用域（0〜2³¹未満の値・0〜31 のシフト量）では従来と一致しますが、`bit32.lshift(1, 32)` のような範囲外の特殊ケースのみ結果が変わります（ネイティブ＝0、従来＝1）。
- `math.random` は両エンジンとも毎フレーム `frame` でシードし直す決定論的乱数ですが、乱数列の実装が異なるためエンジンをまたぐと同じシードでも値が変わります。エンジン非依存の乱数が必要な場合は `anim.rand(min, max, seed)` を使用してください。
 
> 高速ランタイム（`luajit.exe` / `lua51.dll` 等）はプラグインに同梱され、本体と同じ場所の `native` フォルダーへ自動配置されます。手動インストールは不要です。
 
### 実行スレッドとタイムアウト
 
スクリプトはエフェクトインスタンスごとに確保された専用のバックグラウンドスレッド上で実行されます。実行要求は容量 1 のキューを介してこのスレッドへ渡され、メイン側は実行完了またはタイムアウトまで待機します。
 
スクリプトの実行タイムアウトは **5000ms（5秒）** です。タイムアウトに達した場合、MoonSharp のデバッガフック経由でスクリプトの実行が協調的にキャンセルされ、エラーとして扱われます。タイムアウトしたスレッドは破棄され、新しい実行スレッドに差し替えられます。無限ループとなるスクリプトを書かないよう注意が必要です。
 
コンパイル済みのスクリプトはキャッシュされ、スクリプトの内容（文字列）が前回実行時から変化していない場合は再コンパイルを行いません。
 
### 実行結果のキャッシュ
 
フレーム番号・時間・尺・フレームレート・各トラックバー値・スクリプト本文・タイムラインの位置情報・入力画像の描画情報などをまとめたキー（RenderKey）を毎回算出し、前回フレームと完全に一致する場合はスクリプトの再実行自体をスキップして、前回の出力結果をそのまま再利用します。
 
スクリプトの実行中に `LuaScriptCompilationException` または `LuaScriptRuntimeException` が発生した場合は、ログへ出力したうえでエフェクトの出力を入力そのまま（変形・ピクセル変更なし）にフォールバックします。
 
### グローバル変数のリセットと整合性チェック
 
内部の Lua 実行環境（Script / Table インスタンス）は性能のためフレームをまたいで再利用されますが、スクリプトが定義したユーザー定義のグローバル変数や、`obj` / `scene` / `anim` / `ymm4` テーブルへスクリプトが追加したキーは、各フレームの実行開始時に初期状態のスナップショットと比較して取り除かれます。これにより、あるフレームでスクリプトが作成した状態が次のフレームへ意図せず持ち越されることを防いでいます。
 
また、実行のたびに組み込みの `math` グローバルがテーブル型のまま残っているかを確認し、スクリプトの誤操作などで破壊されていた場合は Lua 実行環境ごと再構築します。
 
`math.random` は、毎フレームの実行直前に `math.randomseed(frame)` が呼び出されるため、同じフレーム番号であれば常に同じ乱数列となる決定論的な挙動です。フレームごとに異なる値が必要な場合や真にランダムな値が必要な場合は、`anim.rand(min, max, seed)` に任意のシード値を渡して利用することを検討してください。
 
---
 
## パラメータ一覧
 
| パラメータ名 | 型 | デフォルト | スライダー表示範囲 | アニメーション | 説明 |
|---|---|---|---|---|---|
| **Track0** | 数値 | 0.00 | -100 〜 100 | ✔ | `obj.track0` として参照できる汎用パラメータ |
| **Track1** | 数値 | 0.00 | -100 〜 100 | ✔ | `obj.track1` として参照できる汎用パラメータ |
| **Track2** | 数値 | 0.00 | -100 〜 100 | ✔ | `obj.track2` として参照できる汎用パラメータ |
| **Track3** | 数値 | 0.00 | -100 〜 100 | ✔ | `obj.track3` として参照できる汎用パラメータ |
 
---
 
## 使い方ガイド
 
### 基本的な使い方
 
1. タイムライン上のアイテムを選択し、映像エフェクトを追加します。
2. 映像エフェクトの種類として **「Luaスクリプト」** を選択します。初期状態ではデフォルトスクリプト `obj.alpha = math.min(time * 255, 255)` が設定されており、フェードインの効果になっています。
3. プロパティパネルのスクリプトコード欄に Lua スクリプトを記述します。
4. 必要に応じて Track0〜Track3 にキーフレームを設定し、スクリプト内の `obj.track0`〜`obj.track3` を通じて値を参照します。
### obj テーブルでオブジェクトを制御する
 
```lua
-- フェードアウト
obj.alpha = math.max((obj.totalframe - obj.frame) / obj.framerate * 255, 0)
 
-- 時間に応じて回転（1秒で90度）
obj.rz = time * 90
 
-- Track0 でズームを制御（1.0 = 等倍）
obj.zoom = obj.zoom * (1 + obj.track0 / 100)
```
 
### ピクセル単位で画像を加工する
 
```lua
-- 赤成分を反転する
for y = 0, obj.h - 1 do
    for x = 0, obj.w - 1 do
        local r, g, b, a = obj.getpixel(x, y)
        obj.setpixel(x, y, 255 - r, g, b, a)
    end
end
```
 
`getpixeldata` を使うと、より高速にピクセルを処理できます。
 
```lua
local pd = obj.getpixeldata()
local w = pd.width
local h = pd.height
for y = 0, h - 1 do
    for x = 0, w - 1 do
        local base = (y * w + x) * 4
        local r = pd:get(base + 1)
        local g = pd:get(base + 2)
        local b = pd:get(base + 3)
        local gray = r * 0.299 + g * 0.587 + b * 0.114
        pd:set(base + 1, gray)
        pd:set(base + 2, gray)
        pd:set(base + 3, gray)
    end
end
```
 
### anim ユーティリティで動きを作る
 
```lua
-- 1秒周期でゆっくり明滅させる
obj.alpha = anim.oscillate(time, 0, 255, 1)
 
-- イーズアウトで0.5秒かけて出現させる
local t = anim.duration(time, 0.5)
obj.alpha = anim.ease_out(t) * 255
```
 
### スクリプトの保存・読み込み
 
スクリプトエディタ上部のツールバーから、作成したスクリプトを `.lua` ファイルとしてエクスポートしたり、既存の `.lua` ファイルをインポートしたりできます。
 
---
 
## 注意事項
 
- **AviUtl 互換は部分的**: AviUtl のアニメーション効果スクリプトのうち、変形・不透明度・乱数の範囲のみ実行できます（[AviUtl スクリプトの部分互換](#9-aviutl-スクリプトの部分互換)を参照）。描画・フィルタ・ファイルアクセス系の関数、および AviUtl 向けの EXO 出力（`.exo`）には対応していません。
- **実行タイムアウト**: スクリプトの実行は 5000ms（5秒）でタイムアウトし、超過した場合は強制終了されエラーとして扱われます。無限ループが発生しないよう注意してください。
- **利用可能な標準ライブラリの制限**: `table`、`string`、`math`、`bit32` および基本関数のみ利用できます。`io`、`os`、`debug`、`ffi`、`require` は利用できません。
- **ピクセル操作の負荷**: `obj.getpixel` / `obj.setpixel` / `obj.getpixeldata` は実際の入力画像にアクセスするため、各フレームで最初に呼び出した際に GPU→CPU へのデータ転送が発生します（GPU カーネルはこの転送を行いません）。不要な場合は呼び出しを省略することで処理を高速化できます。これらを含むスクリプトは内容に応じて CPU カーネル・GPU カーネル・高速ランタイム（LuaJIT）のいずれかで自動実行され大幅に高速化されます。詳細は[実行エンジンの自動振り分け](#実行エンジンの自動振り分け)を参照。
- **`math.random` はフレームごとの決定論的な乱数**: 毎フレーム `frame` の値でシードし直されるため、同一フレームでは常に同じ値になります。フレームをまたいで変化する乱数的な値が必要な場合は `anim.rand` の利用を検討してください。
- **`obj.oz` は出力に反映されません**: スクリプト実行後にコンテキストへ読み戻されますが、現在の実装では中心点オフセットの出力計算に使用されていません。
- **エクスポートは先頭の1件のみ**: 複数アイテムを選択した状態でも、エクスポートされるスクリプトは先頭のアイテムのもののみです（インポート・クリアは選択中のすべてのアイテムに適用されます）。
- **構文ハイライトと折り畳みの対応範囲の違い**: 構文ハイライトのブロックコメント認識は `--[[ ]]` 形式に限られますが、コード折り畳み機能は `--[==[ ]==]` のようなレベル付き長括弧のコメント・文字列にも対応しています。

---
 
## 免責事項
 
本プラグインは MIT ライセンスのもとで公開されています。
 
**本ソフトウェアは「現状のまま」提供されており、明示・黙示を問わず、商品性、特定目的への適合性、および権利非侵害に関する保証を含む、いかなる種類の保証も行いません。**
 
作者は、本プラグインの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。
ご利用は自己責任でお願いします。

---
 
## サードパーティライセンス
 
本プラグインは以下のサードパーティソフトウェアを同梱・使用しています。各ライセンスの全文は、配布パッケージ（`.ymme`）内の `LICENSE` フォルダーおよびリポジトリの [`.github/LICENSE`](.github/LICENSE) に収録しています。
 
| ソフトウェア | 用途 | ライセンス |
|---|---|---|
| [MoonSharp](https://github.com/moonsharp-devs/moonsharp) | 標準のLua実行エンジン（Lua 5.2 互換） | 3-Clause BSD License |
| [LuaJIT](https://github.com/LuaJIT/LuaJIT) | ピクセル操作向け高速ランタイム（`native/luajit.exe`・`lua51.dll`） | MIT License |
 
### MoonSharp（3-Clause BSD License）
 
```
Copyright (c) 2014-2016, Marco Mastropaolo
All rights reserved.
 
Parts of the string library are based on the KopiLua project (https://github.com/NLua/KopiLua)
Copyright (c) 2012 LoDC
 
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
 
* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.
* Neither the name of the {organization} nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.
 
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
 
### LuaJIT（MIT License）
 
同梱の高速ランタイム（`native/luajit.exe`・`native/lua51.dll`）は LuaJIT v2.1 です。LuaJIT 本体は MIT License で配布されており、その中に Lua 5.1/5.2（Copyright © 1994-2012 Lua.org, PUC-Rio、MIT License）および dlmalloc（パブリックドメイン）のコードを含みます。以下は LuaJIT 本体の著作権表示です（Lua・dlmalloc を含む全文は `LICENSE/LuaJIT.txt` を参照）。
 
```
LuaJIT -- a Just-In-Time Compiler for Lua. https://luajit.org/

Copyright (C) 2005-2026 Mike Pall. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
 
---
 
## ライセンス
 
[MIT License](LICENSE.txt)
