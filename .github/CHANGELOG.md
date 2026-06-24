# v1.1.1 - Lua スクリプト for YMM4

特定のテーマでスクリプトエディタを開いた際に発生する例外を修正したリリースです。

---

## 修正

### 1. テーマに対応するシンタックス定義の不足による例外

YMM4 のテーマが「Windows」または「Black」のとき、スクリプトエディタの表示時に
`System.IO.IOException` が発生する不具合を修正しました。

スクリプトエディタ (`CodeEditor`) は現在のテーマ名から
`Lua-{テーマ名}.xshd` を解決しますが、本プラグインは `Lua-Dark.xshd` と
`Lua-Light.xshd` のみを同梱していたため、テーマ名が `Windows` や `Black` の場合に
対応するシンタックス定義が見つからず例外となっていました。

不足していた以下の 2 ファイルを追加しました。

| ファイル | 配色 | 対応テーマ |
|---|---|---|
| `Lua-Black.xshd` | 暗背景向け | Black |
| `Lua-Windows.xshd` | 明背景向け | Windows |

配色は YMM4 のテーマの明暗（Black は暗背景、Windows は明背景）に合わせ、
既存の `Lua-Dark.xshd`・`Lua-Light.xshd` と統一しています。これにより
Black・Dark・Light・Windows のいずれのテーマでもシンタックスハイライトが
適用されます。

### 2. プロジェクトファイルへのリソース登録

追加した 2 ファイルを `LuaScript.csproj` に `Resource` として登録しました。
シンタックス定義は pack URI 経由でアセンブリに埋め込まれて参照されるため、
配布パッケージは DLL のみで完結します。
