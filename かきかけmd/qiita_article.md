
# UnityのComputeShaderでsin/cosの精度を調べたら意外な結果になった話

## はじめに

UnityのComputeShaderで三角関数（`sin`, `cos`）を扱う際、その計算精度について深く考えたことはありますか？

GPUは膨大な並列計算を高速に実行するために、一部の数学関数の計算において、CPUとは異なる近似アルゴリズムを用いることがあります。これにより、パフォーマンスと精度の間にトレードオフが生まれます。

本記事では、UnityのC#スクリプトとComputeShaderを使い、CPU（C#の`System.Math`）とGPU（HLSLの`sin`, `cos`）の計算結果をULP（Units in the Last Place）という単位で比較し、その誤差を可視化する実験を行った結果を共有します。

すると、**「ある特定の入力値において、誤差が異常に大きくなる」**という、一見不可解な現象に遭遇しました。この記事では、その原因と、浮動小数点数およびGPUの計算精度の奥深さについて解説します。

## ULP（Units in the Last Place）とは？

まず、本記事で使う誤差の指標である**ULP**について簡単に説明します。

ULPは「最後の桁の単位」を意味し、2つの隣り合う浮動小数点数値の間の距離を表します。浮動小数点数を整数として解釈し、その差を取ることで計算できます。

例えば、「`1.0f`と`1.000001f`のULP距離はいくつか？」といった形で、2つの数値がビットレベルでどれだけ離れているかを示す、非常に精密な指標です。

今回の実験では、このULPを用いてCPUとGPUの計算結果の差を評価します。一般的に、単精度浮動小数点数（`float`）の数学関数では、数ULP程度の誤差は許容範囲内とされています。

## 実験環境とコード

実験は以下の環境で行いました。

-   **Unity:** (お使いのUnityのバージョンを記入)
-   **GPU:** (お使いのGPUのモデル名を記入, 例: NVIDIA GeForce RTX 3080)

### C#スクリプト：`SinCosPrecisionChecker.cs`

CPU側での基準値計算、GPUへのデータ転送、結果の比較を行うC#スクリプトです。
`Start()`メソッドで`RunPrecisionCheck()`を呼び出し、精度の評価を行います。

```csharp
// SinCosPrecisionChecker.cs
using UnityEngine;

public class SinCosPrecisionChecker : MonoBehaviour
{
    public ComputeShader computeShader;
    [Range(1, 100000)]
    public int numSamples = 10000;

    // (中略) ... ULP計算のための構造体やメインロジック ...

    void RunPrecisionCheck()
    {
        // 1. 入力データをランダムに生成 (-PIからPIの範囲)
        // 2. ComputeShaderにデータを送り、GPUでsin/cosを計算
        // 3. GPUから結果を受け取る
        // 4. C#のSystem.Math.Sin/Cosで計算した「基準値」と比較
        // 5. ULP誤差を計算し、最大誤差や特定のケースをログに出力
    }

    long CalculateUlpDistance(float a, float b)
    {
        // 浮動小数点数のビット表現を整数に変換し、その差を計算する
        // (記事の前半で提示されたロバストなULP計算コードをここに貼り付け)
    }
}
```

### ComputeShader：`SinCosPrecision.compute`

GPU側で実際に`sin`と`cos`を計算するシェーダーコードです。非常にシンプルです。

```hlsl
// SinCosPrecision.compute
#pragma kernel CSMain

struct Input { float theta; };
struct Output { float sin_value; float cos_value; };

StructuredBuffer<Input> _InputBuffer;
RWStructuredBuffer<Output> _OutputBuffer;

[numthreads(8, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    float theta = _InputBuffer[id.x].theta;
    _OutputBuffer[id.x].sin_value = sin(theta);
    _OutputBuffer[id.x].cos_value = cos(theta);
}
```

## 驚きの実験結果：ULP誤差600超え！？

実験を実行すると、ほとんどの場合は数ULP程度の小さな誤差に収まりました。しかし、時折、コンソールに以下のような警告が表示されました。

```
Sin ULP: 657, Ref: -0.0013264732 (bits: BAADDD0E), GPU: -0.00132654968 (bits: BAADDF9F)
```

**ULP誤差が657**。これは単なる計算精度の違いと呼ぶには、あまりにも大きな値です。一体何が起きているのでしょうか？

さらに調査を進めると、この巨大な誤差には再現性があり、特定の条件で発生することがわかりました。

-   **`sin(theta)`の誤差**: 入力値`theta`が**0に非常に近い**とき（例: `-0.0013...`）に最大化する。
-   **`cos(theta)`の誤差**: 入力値`theta`が**π/2に非常に近い**ときに最大化する。

共通しているのは、**三角関数の返り値が0に非常に近くなる**タイミングで、ULP誤差が爆発的に増大する、という点です。

## なぜ「0に近い値」でULP誤差が大きくなるのか？

この現象の鍵は、**浮動小数点数の性質**そのものにあります。

単精度浮動小数点数（`float`）は、32ビットのデータを使って数値を表現します。その構造上、表現できる数値の**密度（精度）は一様ではありません**。

-   **0から遠い（絶対値が大きい）領域**: 数値の分布は**粗い**。隣り合う数値との間隔は広い。
-   **0に近い（絶対値が小さい）領域**: 数値の分布は**非常に密**。隣り合う数値との間隔は極めて狭い。

![浮動小数点数の密度イメージ](https://i.stack.imgur.com/h5L2d.png)
*(画像引用: https://stackoverflow.com/questions/34366259/what-is-the-spacing-of-ieee-754-single-precision-numbers )*

今回のULP計算は、このビットレベルの間隔を数えています。

GPUの`sin`関数が持つ絶対的な計算誤差（例: `0.00000008`）は、入力値によらず比較的一定だと仮定します。

-   **`sin(1.5)` のように結果が1に近い場合**:
    -   この領域のULP間隔は比較的広い。
    -   絶対誤差`0.00000008`は、ULPに換算すると数個分にしかならない。
    -   結果: **ULP誤差は小さい。**

-   **`sin(0.001)` のように結果が0に近い場合**:
    -   この領域のULP間隔は極めて狭い。
    -   同じ絶対誤差`0.00000008`でも、この狭い間隔を何百個も飛び越えてしまう。
    -   結果: **ULP誤差は非常に大きくなる。**

つまり、今回観測された「巨大なULP誤差」は、**GPUの計算が特別不正確だったわけではなく、評価指標であるULPの特性と、浮動小数点数の0近傍における密度の高さに起因する、ある意味で「正常」な現象だった**のです。

## まとめと考察

今回の実験から、以下のことがわかりました。

1.  GPUの`sin`/`cos`関数は、CPUの計算結果と比較すると、確かに数ULPの誤差を持つ。
2.  この誤差は、関数の返り値が0に近づくにつれて、ULPという指標上では劇的に増大して見える。
3.  これは浮動小数点数の0近傍における表現密度の高さに起因するものであり、GPUの計算が本質的に破綻しているわけではない。

普段何気なく使っている`sin`や`cos`ですが、その裏側ではハードウェアの特性に合わせた近似計算が行われており、その評価には浮動小数点数そのものへの深い理解が必要であることを再認識させられました。

グラフィックスのレンダリングなど、視覚的な結果に大きな影響を与えない限り、この誤差は問題にならないことがほとんどです。しかし、GPUを物理シミュレーションや科学技術計算に応用する（GPGPU）際には、このような関数の精度特性を十分に理解し、必要であればテイラー展開などを用いた自前の高精度な関数を実装するなどの対策が求められるでしょう。

この記事が、皆さんのGPUコンピューティングに対する理解を深める一助となれば幸いです。
