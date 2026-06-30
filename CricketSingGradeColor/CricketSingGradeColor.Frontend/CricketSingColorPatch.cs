using System;
using HarmonyLib;
using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// Recolours the catch-cricket chirp ripple to reflect the call's derived "sound grade".
    /// The base game draws the ripple as a hard-coded white <c>CImage</c> in
    /// <c>ViewCatchCricket.ShowCricketSingImage</c> and fades its alpha out; we override only the RGB
    /// (preserving alpha) right after, so the original DOFade is unaffected. SingPitch/SingSize are
    /// stable per place, so each place keeps a single colour for its whole appearance.
    /// </summary>
    [HarmonyPatch(typeof(ViewCatchCricket), "ShowCricketSingImage")]
    internal static class CricketSingColorPatch
    {
        private static void Postfix(ViewCatchCricket.CricketPlaceInfo place)
        {
            try
            {
                Apply(place);
            }
            catch (Exception ex)
            {
                Debug.LogError("[CricketSingGradeColor] Failed to colour cricket sing ripple: " + ex);
            }
        }

        private static void Apply(ViewCatchCricket.CricketPlaceInfo place)
        {
            if (place?.PlaceView == null)
            {
                return;
            }

            RectTransform singImage = place.PlaceView.CricketSingImage;
            if (singImage == null)
            {
                return;
            }

            CImage image = singImage.GetComponent<CImage>();
            if (image == null)
            {
                return;
            }

            RippleColorizer.ColorizeForCatch(image, place.SingPitch, place.SingSize);
        }
    }
}
