using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Read-only pointer diagnostics for proving which UI objects a real mouse
    /// input would hit. All methods run on the Unity main thread.
    /// </summary>
    internal static class PointerInspector
    {
        public static Dictionary<string, object> Snapshot(float? x, float? y, string origin, int max)
        {
            var mouse = Input.mousePosition;
            var point = mouse;
            bool usingOverride = x.HasValue && y.HasValue;
            if (usingOverride)
            {
                point = new Vector2(x.Value, y.Value);
                if (string.Equals(origin, "top-left", StringComparison.OrdinalIgnoreCase))
                    point.y = Screen.height - point.y;
            }

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["screen"] = new Dictionary<string, object>
                {
                    ["width"] = Screen.width,
                    ["height"] = Screen.height,
                    ["dpi"] = Screen.dpi,
                },
                ["mousePosition"] = Vec(mouse),
                ["queryPosition"] = Vec(point),
                ["origin"] = usingOverride ? (origin ?? "bottom-left") : "current-mouse",
                ["hits"] = Raycast(point, Math.Max(1, max)),
            };
        }

        private static List<object> Raycast(Vector2 position, int max)
        {
            var hits = new List<object>();
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return hits;

            var eventData = new PointerEventData(eventSystem)
            {
                position = position,
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, raycastResults);
            for (int i = 0; i < raycastResults.Count && hits.Count < max; i++)
            {
                var hit = raycastResults[i];
                hits.Add(new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["gameObject"] = hit.gameObject != null ? hit.gameObject.name : null,
                    ["path"] = hit.gameObject != null ? BuildPath(hit.gameObject.transform) : null,
                    ["module"] = hit.module != null ? hit.module.GetType().FullName : null,
                    ["distance"] = hit.distance,
                    ["depth"] = hit.depth,
                    ["sortingLayer"] = hit.sortingLayer,
                    ["sortingOrder"] = hit.sortingOrder,
                    ["screenPosition"] = Vec(hit.screenPosition),
                    ["worldPosition"] = Vec(hit.worldPosition),
                });
            }
            return hits;
        }

        private static Dictionary<string, object> Vec(Vector2 v)
            => new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y };

        private static Dictionary<string, object> Vec(Vector3 v)
            => new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };

        private static string BuildPath(Transform transform)
        {
            if (transform == null) return "";
            var parts = new List<string>();
            for (var cur = transform; cur != null; cur = cur.parent)
            {
                parts.Add(cur.name);
                if (cur.GetComponent<Canvas>() != null) break;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
