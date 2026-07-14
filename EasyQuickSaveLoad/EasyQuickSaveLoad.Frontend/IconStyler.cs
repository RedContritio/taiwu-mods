using System;
using FrameWork.UISystem.UIElements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EasyQuickSaveLoad.Frontend
{
    internal enum IconKind { Rename, Delete }

    /// <summary>裸图标按钮的悬停反馈：鼠标进入→图标高亮(暖金)并轻微放大，离开→复原。裸图标把按钮方框设成
    /// 透明后，Selectable 自带的悬停高亮就看不见了，故自绘一份挂在按钮上(与按钮点击互不影响)。</summary>
    internal sealed class IconHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Graphic Target;
        public Color Rest = Color.white;
        public Color Hover = new Color(1f, 0.9f, 0.55f, 1f);
        public float HoverScale = 1.15f;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Target == null) return;
            Target.color = Hover;
            Target.rectTransform.localScale = Vector3.one * HoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Target == null) return;
            Target.color = Rest;
            Target.rectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// Swaps a cloned CButton's text label for the game's native rename / delete icon sprite (fetched by
    /// name from the game's atlas via AtlasInfo). Sprite names are discovered from the live RecordSelect UI
    /// and injected into <see cref="RenameSpriteName"/> / <see cref="DeleteSpriteName"/>. If no name is set
    /// (or the sprite can't be resolved) the button keeps its text label, so the UI degrades gracefully.
    /// </summary>
    internal static class IconStyler
    {
        // Native icon sprite names, read off the live RecordSelect record item (ButtonRename / ButtonDelete
        // CImage.sprite). Empty → keep the text label (graceful fallback).
        internal static string RenameSpriteName = "ui9_btn_building_manage_naming_0";
        internal static string DeleteSpriteName = "ui9_btn_newgame_archive_button_close_0";

        public static void Apply(CButton btn, IconKind kind)
        {
            if (btn == null) return;
            string spriteName = kind == IconKind.Rename ? RenameSpriteName : DeleteSpriteName;
            if (string.IsNullOrEmpty(spriteName)) return; // no native icon known — keep text

            try
            {
                var atlas = AtlasInfo.Instance;
                if (atlas == null) return;
                atlas.GetSprite(spriteName, sprite =>
                {
                    if (sprite == null || btn == null) return;
                    SetIcon(btn.gameObject, sprite);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] icon apply failed: " + ex.Message);
            }
        }

        /// <summary>Hide the button's text and show a single centered Image with the icon sprite.</summary>
        private static void SetIcon(GameObject buttonGo, Sprite sprite)
        {
            // Blank the text component so only the icon shows.
            SystemOptionButtonInjector.SetLabelViaReflection(buttonGo, string.Empty);

            // Reuse an existing child "Icon" Image if the native button has one; else create one.
            var iconTf = buttonGo.transform.Find("EQSL_Icon");
            Image img;
            if (iconTf != null)
            {
                img = iconTf.GetComponent<Image>();
            }
            else
            {
                var go = new GameObject("EQSL_Icon", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(buttonGo.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                var inset = 3f; // 收紧内边距：让图标本体几乎填满按钮(≈标题字号)，而非缩在中间一小点
                rt.offsetMin = new Vector2(inset, inset);
                rt.offsetMax = new Vector2(-inset, -inset);
                img = go.GetComponent<Image>();
                img.raycastTarget = false; // clicks go to the button
                img.preserveAspect = true;
            }
            if (img != null)
            {
                img.sprite = sprite;
                // 禁用态(如空档的删除)把裸图标调暗，呼应原生"不可用置灰"——方框没了，改由图标本体承载置灰。
                var sel = buttonGo.GetComponent<UnityEngine.UI.Selectable>();
                bool disabled = sel != null && !sel.interactable;
                img.color = disabled ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                // 悬停反馈(仅可用时)：进入高亮+放大，离开复原。裸图标丢了 Selectable 高亮，故自绘。
                var hover = buttonGo.GetComponent<IconHoverEffect>() ?? buttonGo.AddComponent<IconHoverEffect>();
                hover.Target = img;
                hover.Rest = img.color;
                hover.Hover = disabled ? img.color : new Color(1f, 0.9f, 0.55f, 1f);
            }

            // 呈现对齐游戏原生：原生的改名/删除是【裸图标】，不是带方框的按钮。这里把克隆按钮自带的
            // 方框底图/边框(Image/CImage)设为全透明——alpha=0 仍保留 raycast，所以按钮照样可点，
            // 但只剩那张原生图标可见，配色/形态即与游戏一致。图标本体(EQSL_Icon)不动。
            foreach (var g in buttonGo.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || g.gameObject.name == "EQSL_Icon") continue;
                if (g.GetType().Name.Contains("Image"))
                {
                    var c = g.color;
                    c.a = 0f;
                    g.color = c;
                }
            }
        }
    }
}
