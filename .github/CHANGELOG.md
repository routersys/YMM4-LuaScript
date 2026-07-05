# v1.20.2 - Lua スクリプト for YMM4

[fast] マーカーに実効性を戻すリリースです。v1.20.1 で obj.fill / obj.convolve / obj.resize の GPU 委譲を自動判定に統一した結果、[fast] は委譲の意図を明示するだけの補助マーカーとなり、付けても付けなくても同じ判定を通るため動作に差がありませんでした。本バージョンから [fast] は処理量のしきい値を無視して強制的に GPU へ委譲します。GPU が使えない環境や、GPU の結果が CPU と一致しないと判定された環境では、[fast] を付けても自動で CPU 実行に戻るため、結果が壊れることはありません。

---

## 変更

### [fast] はしきい値を無視して GPU 委譲を強制

obj.fill / obj.convolve / obj.resize は、v1.20.1 と同じく処理量がしきい値以上で GPU の結果が CPU と一致すると確認できたときに自動で GPU へ委譲します。呼び出しの直前に [fast] を置いたときだけ、この処理量のしきい値を無視します。

- しきい値のみを無視し、Direct3D 11 のハードウェアデバイスが利用できることと、GPU の結果が CPU 基準とバイト単位で一致することの確認は、[fast] を付けても変わらず行います。どちらかが成立しない環境では CPU 実装へ戻ります。
- convolve のカーネルサイズの上限(31×31)など、GPU 側の対応可否そのものに関わる制約は [fast] でも変わりません。
- [fast] を付けない呼び出しは v1.20.1 までと同じく、しきい値未満の小さな処理では GPU へ委譲しません。

---

## 互換性・後方互換

- [fast] を付けない呼び出しの動作は v1.20.1 と同一です。
- GPU が使えない環境や検証に通らないカーネルでは、[fast] を付けた呼び出しも CPU 実装が結果を保証するため、実行結果が変わることはありません。
- 関数の引数・戻り値・エラーの扱い、構文・置ける位置(単独の行または同じ行)、3 関数以外へ付けたときのエラー表示に変更はありません。
- エンジンの自動振り分け、エンジン指定ディレクティブの仕様と優先順位、カーネル化の対象、v1.20.1 までの構文拡張と既存の API に変更はありません。

---

## 内部実装

- IPixelBufferProcessor.TryFill / TryConvolve / TryResize に force 引数を追加しました。GpuFillOperation / GpuConvolveOperation / GpuResizeOperation は force が真のときだけ内部のしきい値判定を飛ばし、ハードウェア対応の確認と CPU/GPU 一致検証はそのまま行います。
- 従来エンジンは AviUtlScriptContext.FillBuffer / Convolve / Resize と LuaScriptEngine の FillCore / ConvolveCore / ResizeCore に force 引数を復元し、__fast_fill / __fast_convolve / __fast_resize が force: true を、obj.fill / obj.convolve / obj.resize が force: false を渡します。
- 高速ランタイムは worker.lua の fillImpl / convolveImpl / resizeImpl に force 引数を復元しました。__fast_* 側は force: true、obj.* 側は force: false を渡し、force が真のときは worker.lua 側のしきい値判定も飛ばして常にホストへ照会します。ホストとの往復では cbResult の未使用スロット(10 番目)で force フラグを伝え、LuaJitWorker が読み取って IPixelBufferProcessor へ引き渡します。プロトコルの新しい定数は追加していません。
- テストは 3404 件です。v1.20.1 の 3400 件から、__fast_* がしきい値未満でもホストプロセッサへ委譲されることの検証を両レーンに 3 件ずつ、標準呼び出しが force: false を渡すことの検証を追加しました。ソリューションは x64 で 0 個の警告、0 個のエラーです。
- ドキュメントは付属の 8 言語の [fast] の節、サイトの構文拡張と obj の関数のページ、README を強制委譲の仕様へ更新しました。
