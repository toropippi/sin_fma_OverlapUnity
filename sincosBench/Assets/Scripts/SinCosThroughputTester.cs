using UnityEngine;
using System.Diagnostics;          // Stopwatch

public class SinCosThroughputStopwatch : MonoBehaviour
{
    public ComputeShader shader;
    int threadGroups = 1024;
    int iterations = 65536;

    string resultText = "計測中…";

    void Start()
    {
        Measure("Bench1");
        Measure("Bench2");
        Measure("Bench3");
    }

    void Measure(string kernelName)
    {
        int k = shader.FindKernel(kernelName);
        var buf = new ComputeBuffer(1, sizeof(float));

        shader.SetBuffer(k, "result", buf);
        shader.SetInt("iterations", iterations);

        // --- 計測開始 ---
        var sw = Stopwatch.StartNew();

        shader.Dispatch(k, threadGroups, 1, 1);

        // GPU が終わるまで CPU を同期。要素 1 なので負荷は極小
        var tmp = new float[1];
        buf.GetData(tmp);

        sw.Stop();
        buf.Release();
        // --- 計測終了 ---

        double ms = sw.Elapsed.TotalMilliseconds;
        ulong ops = (ulong)threadGroups * 1024u * 8u * (ulong)iterations;
        double gops = ops / (ms * 1e6);   // G‑ops/sec 相当

        string line = $"{kernelName}: {ms:F3} ms   {gops:F2} G ops/s";
        UnityEngine.Debug.Log(line);
        resultText += "\n" + line;
    }

    int cnt = 0;
    void Update()
    {
        cnt++;
        if (cnt % 60 == 0) // 1 秒ごとに更新
        {
            // 画面上の表示を更新
            resultText = "計測結果:\n";
            Measure("Bench1");
            Measure("Bench2");
            Measure("Bench3");
        }
    }
    void OnGUI()
    {
        GUIStyle st = GUI.skin.label;
        st.fontSize = 16;
        st.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 12, 600, 360), resultText, st);
    }
}
