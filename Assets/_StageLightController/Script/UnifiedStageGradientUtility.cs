using System.Collections.Generic;
using UnityEngine;

public static class UnifiedStageGradientUtility
{
    private const float TimeEpsilon = 0.0001f;

    public static Gradient CreateDefaultBeamLengthGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.black, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

    public static Gradient CloneGradient(Gradient source)
    {
        if (source == null)
            return null;

        var clone = new Gradient();
        clone.SetKeys((GradientColorKey[])source.colorKeys.Clone(), (GradientAlphaKey[])source.alphaKeys.Clone());
        clone.mode = source.mode;
        return clone;
    }

    public static Gradient CloneOrDefaultBeamLengthGradient(Gradient source)
    {
        return source != null ? CloneGradient(source) : CreateDefaultBeamLengthGradient();
    }

    public static void CopyGradientInto(Gradient source, Gradient destination)
    {
        if (destination == null)
            return;

        if (source == null)
            source = CreateDefaultBeamLengthGradient();

        GradientColorKey[] sourceColorKeys = GetUsableColorKeys(source);
        var colorKeys = new GradientColorKey[sourceColorKeys.Length];
        var alphaKeys = new GradientAlphaKey[sourceColorKeys.Length];
        for (int i = 0; i < sourceColorKeys.Length; i++)
        {
            colorKeys[i] = new GradientColorKey(ForceOpaque(sourceColorKeys[i].color), sourceColorKeys[i].time);
            alphaKeys[i] = new GradientAlphaKey(1f, sourceColorKeys[i].time);
        }

        destination.SetKeys(colorKeys, alphaKeys);
        destination.mode = source.mode;
    }

    public static Gradient CreateTintedBeamGradient(Gradient beamLengthGradient, Color tint)
    {
        Gradient source = beamLengthGradient ?? CreateDefaultBeamLengthGradient();

        GradientColorKey[] sourceColorKeys = GetUsableColorKeys(source);

        var colorKeys = new GradientColorKey[sourceColorKeys.Length];
        var alphaKeys = new GradientAlphaKey[sourceColorKeys.Length];
        for (int i = 0; i < sourceColorKeys.Length; i++)
        {
            Color color = MultiplyRgb(sourceColorKeys[i].color, tint);
            colorKeys[i] = new GradientColorKey(color, sourceColorKeys[i].time);
            alphaKeys[i] = new GradientAlphaKey(1f, sourceColorKeys[i].time);
        }

        var result = new Gradient();
        result.SetKeys(colorKeys, alphaKeys);
        result.mode = source.mode;
        return result;
    }

    public static Gradient CreateSolidGradient(Color color)
    {
        color = ForceOpaque(color);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

    public static Gradient LerpGradients(Gradient from, Gradient to, float t)
    {
        if (from == null)
            return CloneOpaqueGradient(to);
        if (to == null)
            return CloneOpaqueGradient(from);

        t = Mathf.Clamp01(t);
        List<float> times = CollectKeyTimes(from, to);
        var colorKeys = new GradientColorKey[times.Count];
        var alphaKeys = new GradientAlphaKey[times.Count];

        for (int i = 0; i < times.Count; i++)
        {
            float time = times[i];
            Color color = Color.Lerp(from.Evaluate(time), to.Evaluate(time), t);
            color = ForceOpaque(color);
            colorKeys[i] = new GradientColorKey(color, time);
            alphaKeys[i] = new GradientAlphaKey(1f, time);
        }

        var result = new Gradient();
        result.SetKeys(colorKeys, alphaKeys);
        result.mode = to.mode;
        return result;
    }

    public static Color MultiplyRgb(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);
    }

    public static Color ForceOpaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private static Gradient CloneOpaqueGradient(Gradient source)
    {
        if (source == null)
            return null;

        var clone = new Gradient();
        CopyGradientInto(source, clone);
        return clone;
    }

    private static GradientColorKey[] GetUsableColorKeys(Gradient gradient)
    {
        GradientColorKey[] colorKeys = gradient != null ? gradient.colorKeys : null;
        if (colorKeys != null && colorKeys.Length > 0)
            return colorKeys;

        return new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };
    }

    private static List<float> CollectKeyTimes(params Gradient[] gradients)
    {
        var times = new List<float> { 0f, 1f };

        foreach (Gradient gradient in gradients)
        {
            if (gradient == null)
                continue;

            foreach (GradientColorKey key in gradient.colorKeys)
                AddUniqueTime(times, key.time);
        }

        times.Sort();
        return times;
    }

    private static void AddUniqueTime(List<float> times, float time)
    {
        time = Mathf.Clamp01(time);
        for (int i = 0; i < times.Count; i++)
        {
            if (Mathf.Abs(times[i] - time) <= TimeEpsilon)
                return;
        }

        times.Add(time);
    }
}
