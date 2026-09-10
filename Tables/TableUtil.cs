using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Table
{
    public static class TableUtil
    {
        public static int[] ParseIntArray(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<int>();
            return raw.Split(',')
                      .Select(s => int.TryParse(s.Trim(), out int result) ? result : 0)
                      .ToArray();
        }

        public static float[] ParseFloatArray(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<float>();
            return raw.Split(',')
                      .Select(s => float.TryParse(s.Trim(), out float result) ? result : 0f)
                      .ToArray();
        }

        public static T[] ParseEnumArray<T>(string raw) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<T>();
            return raw.Split(',')
                      .Select(s => Enum.TryParse(s.Trim(), true, out T result) ? result : default)
                      .ToArray();
        }

        public static T ParseEnum<T>(string raw) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return (T)(object)-1;
            return Enum.TryParse(raw.Trim(), true, out T result) ? result : default;
        }

        public static bool ParseBool(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return false;
            raw = raw.Trim().ToLower();
            return raw == "true" || raw == "1";
        }

        public static Vector2Int ParseVector2Int(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Vector2Int.zero;
            var parts = raw.Replace("[", "").Replace("]", "").Split(',');
            if (parts.Length < 2) return Vector2Int.zero;
            
            int.TryParse(parts[0].Trim(), out int x);
            int.TryParse(parts[1].Trim(), out int y);
            return new Vector2Int(x, y);
        }

        public static Vector2[] ParseVector2Array(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<Vector2>();
            // Handle both "[x,y], [x,y]" and "x,y,x,y" formats
            var cleaned = raw.Replace("[", "").Replace("]", "");
            var parts = cleaned.Split(',');
            
            var results = new List<Vector2>();
            for (int i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 >= parts.Length) break;
                float.TryParse(parts[i].Trim(), out float x);
                float.TryParse(parts[i+1].Trim(), out float y);
                results.Add(new Vector2(x, y));
            }
            return results.ToArray();
        }
    }
}
