# v1.1.0 - Lua スクリプト for YMM4

他のアイテムの描画情報をスクリプトから取得する関数 `obj.getobject` を追加したリリースです。
アイテムの備考をタグとして指定することで、別アイテムの座標・拡大率・回転角・不透明度を
参照できます。発射元アイテムの座標を基準に弾を発射するような演出など、複数アイテムを
連動させる用途に利用できます。

---

## 新機能

### 1. オブジェクト参照関数（obj.getobject）

`obj.getobject(tag)` は、引数 `tag`（文字列）と一致する備考を持つアイテムの描画情報を
テーブルで返却します。`tag` が文字列でない場合、または一致するアイテムが存在しない場合は
`nil` を返却します。

返却テーブルのメンバーは以下のとおりです。

| メンバー | 型 | 説明 |
|---|---|---|
| `exist` | `boolean` | 現在のフレームが対象アイテムの表示期間内にあるか |
| `x`・`y`・`z` | `double` | 描画位置（ピクセル） |
| `zoom` | `double` | 拡大率（1.0 で等倍） |
| `sx`・`sy` | `double` | X・Y 方向の拡大率（`zoom` と同値） |
| `rx`・`ry` | `double` | X・Y 軸回転角（度、常に 0） |
| `rz` | `double` | Z 軸回転角（度） |
| `rxr`・`ryr` | `double` | X・Y 軸回転角（ラジアン、常に 0） |
| `rzr` | `double` | Z 軸回転角（ラジアン） |
| `alpha` | `double` | 不透明度（0〜255、フェード適用済み） |
| `layer` | `int` | レイヤー番号 |

各値は `obj` テーブルと同一の単位系・座標系です。取得できるのはアイテム自身に設定された
アニメーション値であり、グループ制御・カメラ制御・上位エフェクトによる変形は含みません。

同一の `tag` を持つアイテムが複数ある場合は、現在のフレームで表示期間内にあるものを
優先して返却します。表示期間内のものが存在しない場合は最初に一致したアイテムを返却します。

利用例は以下のとおりです。

```lua
local target = obj.getobject("routersys")
if target then
    obj.x = anim.lerp(obj.x, target.x, obj.t)
    obj.y = anim.lerp(obj.y, target.y, obj.t)
end
```

---

## 内部実装

### 2. シーンオブジェクト情報（SceneObjectInfo）

`SceneObjectInfo` は 1 つのアイテムの描画情報を保持する不変の `readonly record struct` です。
`Tag`・`Exist`・`X`・`Y`・`Z`・`Zoom`・`Rz`・`Alpha`・`Layer` を保持し、
値の等価比較によってキャッシュ判定に利用されます。

### 3. エフェクトプロセッサーの拡張（LuaScriptEffectProcessor）

`BuildSceneObjects(EffectDescription desc)` は、`desc.Scenes` から `desc.SceneId` に一致する
シーンを取得し、そのタイムライン上の `VisualItem` のうち備考（`Remark`）が設定されている
ものを列挙します。

各アイテムについて、`desc.TimelinePosition.Frame - item.Frame` を `0`〜`item.Length` に
クランプしたローカルフレームでアニメーション値を評価します。`Exist` は現在フレームが
アイテムの表示期間内にあるかを表します。

値は描画パイプラインと同一の式で算出します。`Zoom` は評価値を 100 で除算した倍率、
`Alpha` は `Opacity` の評価値にフェード係数と 255 を乗じた値です。フェード係数は
フェードイン・フェードアウトの経過割合の小さい方です。

評価はすべて描画スレッド上で行い、結果を不変配列としてスクリプト実行スレッドへ渡します。

`Update` のキャッシュ判定に、このスナップショットの一致比較を `RenderKey` の比較へ加えて
追加しました。参照先のアイテムが移動した場合に出力が再計算されます。

### 4. スクリプト実行コンテキストの拡張（AviUtlScriptContext）

`SceneObjects` プロパティ（`IReadOnlyList<SceneObjectInfo>`）を追加しました。
`TryGetObject(string tag, out SceneObjectInfo info)` は、前述の選択規則に従って
アイテムを検索します。

### 5. Lua 実行エンジンの拡張（LuaScriptEngine）

`obj` テーブルへのネイティブ関数登録を `RegisterObjectCallbacks` に統合し、`obj.getobject` を
追加しました。返却テーブルはコンテキストのスナップショットから構築します。

---

## ドキュメント

対応する 8 言語（ja-jp・en-us・zh-cn・zh-tw・ko-kr・es-es・id-id・ar-sa）のスクリプト
説明文に `obj.getobject` の項目を追記しました。
