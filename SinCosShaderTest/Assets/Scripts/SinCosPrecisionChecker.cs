using UnityEngine;

public class SinCosPrecisionChecker : MonoBehaviour
{
    public ComputeShader computeShader;

    [Range(1, 16777215)]
    public int numSamples = 4194304;

    private struct Input { public float theta; }
    private struct Output { public float sin_value; public float cos_value; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct FloatIntUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public float f;
        [System.Runtime.InteropServices.FieldOffset(0)] public int i;
    }

    void Start()
    {
        if (computeShader == null)
        {
            Debug.LogError("ComputeShaderが設定されていません。");
            return;
        }
        RunPrecisionCheck();
    }

    void RunPrecisionCheck()
    {
        int kernelHandle = computeShader.FindKernel("CSMain");
        Input[] inputData = new Input[numSamples];
        Output[] outputData = new Output[numSamples];

        System.Random rand = new System.Random();
        for (int i = 0; i < numSamples; i++)
        {
            inputData[i].theta = (float)((rand.NextDouble() * 2.0 - 1.0) * System.Math.PI);
        }

        using (ComputeBuffer inputBuffer = new ComputeBuffer(numSamples, sizeof(float)))
        using (ComputeBuffer outputBuffer = new ComputeBuffer(numSamples, sizeof(float) * 2))
        {
            inputBuffer.SetData(inputData);
            outputBuffer.SetData(outputData);

            computeShader.SetBuffer(kernelHandle, "_InputBuffer", inputBuffer);
            computeShader.SetBuffer(kernelHandle, "_OutputBuffer", outputBuffer);
            computeShader.SetInt("_NumSamples", numSamples);

            computeShader.Dispatch(kernelHandle, (numSamples + 63) / 64, 1, 1);

            outputBuffer.GetData(outputData);
        }

        long maxSinUlpError = 0;
        long maxCosUlpError = 0;
        int highErrorCount = 0;
        const int MAX_HIGH_ERROR_LOGS = 5;
        const int ULP_THRESHOLD = 4;
        long totalSinUlpError = 0;
        long totalCosUlpError = 0;

        for (int i = 0; i < numSamples; i++)
        {
            float theta = inputData[i].theta;
            float referenceSin = (float)System.Math.Sin(theta);
            float referenceCos = (float)System.Math.Cos(theta);
            float gpuSin = outputData[i].sin_value;
            float gpuCos = outputData[i].cos_value;

            long sinUlp = CalculateUlpDistance(gpuSin, referenceSin);
            long cosUlp = CalculateUlpDistance(gpuCos, referenceCos);
            totalSinUlpError += sinUlp;
            totalCosUlpError += cosUlp;

            if (sinUlp > maxSinUlpError) maxSinUlpError = sinUlp;
            if (cosUlp > maxCosUlpError) maxCosUlpError = cosUlp;

            if ((sinUlp > ULP_THRESHOLD || cosUlp > ULP_THRESHOLD) && highErrorCount < MAX_HIGH_ERROR_LOGS)
            {
                highErrorCount++;
                Debug.LogWarning($"--- 高い誤差を検出 (ケース #{highErrorCount}) ---");
                Debug.LogWarning($"Theta: {theta}");

                var refSinUnion = new FloatIntUnion { f = referenceSin };
                var gpuSinUnion = new FloatIntUnion { f = gpuSin };
                Debug.LogWarning($"Sin ULP: {sinUlp}, Ref: {referenceSin:G9} (bits: {refSinUnion.i:X8}), GPU: {gpuSin:G9} (bits: {gpuSinUnion.i:X8})");

                var refCosUnion = new FloatIntUnion { f = referenceCos };
                var gpuCosUnion = new FloatIntUnion { f = gpuCos };
                Debug.LogWarning($"Cos ULP: {cosUlp}, Ref: {referenceCos:G9} (bits: {refCosUnion.i:X8}), GPU: {gpuCos:G9} (bits: {gpuCosUnion.i:X8})");
            }
        }

        Debug.Log($"--- sin/cos 精度調査結果 ---");
        Debug.Log($"調査回数: {numSamples} 回");
        Debug.Log($"最大誤差 (sin): {maxSinUlpError} ULP");
        Debug.Log($"最大誤差 (cos): {maxCosUlpError} ULP");
        Debug.Log($"平均誤差 (sin): {totalSinUlpError / numSamples} ULP");
        Debug.Log($"平均誤差 (cos): {totalCosUlpError / numSamples} ULP");
        
        // IEEE754 単精度: 隠れビット込みで 24ビットの有効桁数
        const int totalSignifBits = 24;
        double avgSinUlp = (double)totalSinUlpError / numSamples;
        double avgCosUlp = (double)totalCosUlpError / numSamples;
        double maxSinBits = maxSinUlpError == 0 ? totalSignifBits : totalSignifBits - System.Math.Log(maxSinUlpError, 2);
        double maxCosBits = maxCosUlpError == 0 ? totalSignifBits : totalSignifBits - System.Math.Log(maxCosUlpError, 2);
        double avgSinBits = avgSinUlp == 0 ? totalSignifBits : totalSignifBits - System.Math.Log(avgSinUlp, 2);
        double avgCosBits = avgCosUlp == 0 ? totalSignifBits : totalSignifBits - System.Math.Log(avgCosUlp, 2);

        Debug.Log($"仮数部全{totalSignifBits}bit中の精度:");
        Debug.Log($"  Sin → 最大: {maxSinBits:F2} bit, 平均: {avgSinBits:F2} bit");
        Debug.Log($"  Cos → 最大: {maxCosBits:F2} bit, 平均: {avgCosBits:F2} bit");
        Debug.Log("---------------------------------");
    }

    long CalculateUlpDistance(float a, float b)
    {
        if (!float.IsFinite(a) || !float.IsFinite(b)) return long.MaxValue;
        if (a == b) return 0;

        var uA = new FloatIntUnion { f = a };
        var uB = new FloatIntUnion { f = b };

        int aInt = uA.i < 0 ? unchecked((int)0x80000000) - uA.i : uA.i;
        int bInt = uB.i < 0 ? unchecked((int)0x80000000) - uB.i : uB.i;

        return System.Math.Abs((long)aInt - bInt);
    }
}
