namespace PqrsPlatform.Infrastructure.AI;

public static class EmbeddingMath
{
    public const int TargetDimensions = 1536;

    public static float[] Fit(float[] values, int target = TargetDimensions)
    {
        if (values.Length == target) return values;

        var result = new float[target];

        if (values.Length > target)
        {
            Array.Copy(values, result, target);
            Normalize(result);
            return result;
        }

        Array.Copy(values, result, values.Length);
        return result;
    }

    public static void Normalize(float[] v)
    {
        double sum = 0;
        for (var i = 0; i < v.Length; i++) sum += (double)v[i] * v[i];

        var magnitude = Math.Sqrt(sum);
        if (magnitude < 1e-8) { v[0] = 1f; return; }

        for (var i = 0; i < v.Length; i++) v[i] = (float)(v[i] / magnitude);
    }
}
