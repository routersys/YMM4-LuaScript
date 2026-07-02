# v1.11.2 - Lua スクリプト for YMM4

`tempbuffer` への描画合成を GPU 実行へ置き換えたリリースです。MoonSharp エンジンで
`obj.draw` / `obj.drawpoly` が `tempbuffer` へ行う合成を、CPU のピクセルループから
Direct2D による GPU 合成へ変更しました。GPU を利用できない環境では従来の CPU 合成へ
自動的に切り替わります。あわせて、描画合成の変換行列の計算を一元化し、合成レーンの
ユニットテストを追加しました。

---

## 新機能・機能改善

### 1. tempbuffer への描画合成を GPU 実行へ変更

MoonSharp エンジンで `obj.setoption("drawtarget", "tempbuffer")` を指定した際の
`obj.draw` / `obj.drawpoly` の合成を、GPU で実行するようにしました。

- 合成の意味論は従来と同一です。乗算済みアルファのソースオーバー、不透明度の乗算、
  `antialias` によるリニア・ニアレストの切り替えをそのまま維持しています。
- GPU での合成に失敗した場合は従来の CPU 合成へ自動的に切り替わり、ログへ一度だけ
  警告を出します。切り替え後の動作は従来と完全に同じです。
- 大きな `tempbuffer` へ繰り返し合成するスクリプトで、毎フレームの CPU 負荷が
  下がります。

### 2. 描画合成の変換行列を一元化

`obj.draw` の ox / oy / zoom / aspect と `obj.drawpoly` の 4 頂点から合成の変換行列を
求める処理を 1 箇所へ集約し、`framebuffer` への合成と `tempbuffer` への合成が同じ
定義を共有するようにしました。拡大率が 0 になる描画コマンドは、どちらの描画先でも
合成をスキップします。

### 3. ドキュメントの更新

内蔵ドキュメント（`LuaScript/docs/LuaScript.*.txt`）・README・サイトのドキュメントへ、
`tempbuffer` への合成が MoonSharp では GPU で実行されることと、GPU を利用できない
環境では CPU の合成へ自動的に切り替わることを各言語で追記しました。

---

## 互換性・後方互換

- スクリプトの書き方と API に変更はありません。`drawtarget` の指定方法・描画結果の
  意味論は従来どおりです。
- GPU を利用できない環境では、従来と同じ CPU 合成で動作します。
- GPU 合成は浮動小数点で丸めるため、`tempbuffer` への合成結果が従来の CPU 合成と
  1 階調程度異なることがあります。ネイティブエンジンの `tempbuffer` 合成は従来どおり
  ワーカー内の CPU で実行されるため、丸めの違いによりエンジン間で合成結果がごく僅かに
  異なることがあります。
- `framebuffer` への描画合成は従来どおり GPU で実行され、変更はありません。

---

## 内部実装

- 合成の契約 `IBufferCompositor` を追加し、`AviUtlScriptContext` の `tempbuffer` 合成が
  注入された合成器を使うようにしました。既定は CPU の `SoftwareCompositor` で、
  ネイティブレーンとテストの動作は変わりません。
- `HardwareCompositor` を追加しました。`PixelBufferManager` と同じ手順で、合成元と
  合成先のアップロード、ソースオーバーでの描画、CPU 読み取り可能なステージング
  ビットマップ経由の読み戻しを行います。ビットマップはサイズ単位でキャッシュします。
- `FallbackCompositor` を追加しました。GPU の合成器を優先し、初回の例外で恒久的に
  CPU の合成器へ降格して、通知コールバックを一度だけ呼び出します。
- `SoftwareCompositor` を静的クラスからインスタンスクラスへ変更し、既存の合成処理を
  そのまま契約の実装として保持しました。合成の数学に変更はありません。
- 変換行列の構築を `DrawTransform.TryResolve` へ集約し、`DrawCompositor` と
  `HardwareCompositor` の双方が同じ数学を共有するようにしました。
- `LuaScriptEffectProcessor` の `CreateEffect` とタイムアウト後のコンテキスト再生成で
  合成器を注入するようにしました。
- テストへ `DrawTransformTests`・`FallbackCompositorTests` と、`SoftwareCompositor` の
  `Compose` ディスパッチの検証を追加しました（全 2987 件）。`HardwareCompositor` の
  描画出力はテスト環境で Direct2D を実行できないため、YMM4 上での確認が対象です。
