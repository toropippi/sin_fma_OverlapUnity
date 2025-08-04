# Unity HLSL sin/cos 精度調査プロジェクト

## 概要

このプロジェクトは、Unityのコンピュートシェーダー（HLSL）で実行される `sin` および `cos` 関数の精度を検証することを目的としています。

GPU上で計算された単精度浮動小数点数（`float`）の `sin`/`cos` の結果と、CPU上で計算された倍精度浮動小数点数（`double`）からキャストした「より正確な」値とを比較します。

誤差の評価には**ULP (Units in the Last Place)** という指標を用います。これにより、2つの浮動小数点数がビットレベルでどれだけ離れているかを正確に測定できます。

## 使用方法

1.  Unityで新規プロジェクトを作成します。
2.  `Assets` フォルダ内に `Scripts` と `Shaders` という名前のフォルダを作成します。
3.  `SinCosPrecisionChecker.cs` を `Assets/Scripts` フォルダに配置します。
4.  `SinCosPrecision.compute` を `Assets/Shaders` フォルダに配置します。
5.  Unityエディタで空のシーンを開き、新しい `GameObject` を作成します。
6.  作成した `GameObject` に `SinCosPrecisionChecker.cs` スクリプトをアタッチします。
7.  インスペクター（Inspector）上で、`Sin Cos Precision Checker` コンポーネントの `Compute Shader` フィールドに `SinCosPrecision.compute` シェーダーアセットをドラッグ＆ドロップします。
8.  Unityエディタで `Play` ボタンを押し、シーンを実行します。
9.  `Console` ウィンドウに精度の解析結果が出力されます。

## 解析結果の読み方

コンソールには以下のような情報が出力されます。

-   **調査回数**: テストに使用したランダムな角度の数。
-   **最大誤差 (sin)**: `sin` 計算で観測された最大のULP誤差。
-   **最大誤差 (cos)**: `cos` 計算で観測された最大のULP誤差。

**ULP誤差について**:
-   **0 ULP**: ビットレベルで完全に一致していることを意味します。
-   **1 ULP**: 可能な限り最小の誤差（最終ビットが1だけ異なる）を意味します。

一般的に、最新のGPUにおける `sin`/`cos` 関数の精度は非常に高く、ULP誤差は1または2程度に収まることが期待されます。
