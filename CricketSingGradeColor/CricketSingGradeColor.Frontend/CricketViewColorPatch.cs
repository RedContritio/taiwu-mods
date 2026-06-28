using System;
using System.Collections.Generic;
using System.Reflection;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// Colours the ripple of the cricket "card" views — <c>CricketView</c> and the (deeply nested)
    /// <c>CricketViewNew</c>. These are the components that draw a specific cricket's 叫声圈 in the 斗蛐蛐 box,
    /// the collection jar, item/encyclopedia tooltips, previews and event UIs. Each exposes public
    /// <c>SingPitch</c>/<c>SingSize</c> (int) and a private <c>CImage _singImage</c>, and their <c>Sing(...)</c>
    /// only animates scale + alpha (never sets RGB), so we apply the sound-grade colour as a Postfix.
    ///
    /// Patched manually from <see cref="FrontendPlugin"/> (the types are nested / namespace-uncertain, so we
    /// resolve them by name rather than typeof). One generic Postfix handles both view types via reflection.
    /// The world-map 玩物 (a CricketSing with placeholder SetCricketData(0,0)) is intentionally NOT covered.
    /// </summary>
    internal static class CricketViewColorPatch
    {
        private sealed class Members
        {
            public PropertyInfo Pitch;
            public PropertyInfo Size;
            public FieldInfo Image;
        }

        private static readonly Dictionary<Type, Members> _cache = new Dictionary<Type, Members>();

        public static void Postfix(object __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[CricketSingGradeColor] Failed to colour cricket view ripple: " + ex);
            }
        }

        private static void Apply(object view)
        {
            if (view == null)
            {
                return;
            }

            Members m = Resolve(view.GetType());
            if (m == null)
            {
                return;
            }

            // _singImage is a CImage (a UnityEngine.UI.Graphic).
            var image = m.Image.GetValue(view) as CImage;
            if (image == null)
            {
                return;
            }

            int singPitch = Convert.ToInt32(m.Pitch.GetValue(view, null));
            int singSize = Convert.ToInt32(m.Size.GetValue(view, null));
            RippleColorizer.Colorize(image, singPitch, singSize);
        }

        private static Members Resolve(Type type)
        {
            if (_cache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            Members members = null;
            PropertyInfo pitch = type.GetProperty("SingPitch", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo size = type.GetProperty("SingSize", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo image = FindField(type, "_singImage");
            if (pitch != null && size != null && image != null)
            {
                members = new Members { Pitch = pitch, Size = size, Image = image };
            }

            _cache[type] = members;
            return members;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    return f;
                }
            }
            return null;
        }
    }
}
