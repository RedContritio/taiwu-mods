using System;
using System.Reflection;
using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Views.RecordSelect;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using GameData.Domains.World;
using UnityEngine;
using UnityEngine.UI;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// Self-built, centered modal slot picker for the manual 存档/读档 buttons. Clones the native window
    /// frame + native CButton and lays the configurable N manual slots out in a grid (the quick slot -1 is
    /// independent and NOT shown here). Save mode: click a slot → confirm (overwrite if filled) → save.
    /// Load mode: click a FILLED slot → confirm → load. Each filled slot carries a small 删 button →
    /// confirm → delete. Refreshes after every mutation. A dimmed backdrop captures outside-clicks to close.
    /// Templates are handed in fresh each open (SetTemplates) since the native menu is rebuilt per session.
    /// </summary>
    internal sealed class SlotPickerPanel
    {
        internal enum PickMode { Save, Load }

        private GameObject _frameTemplate;
        private CButton _buttonTemplate;
        private Transform _canvasRoot;

        private GameObject _root;   // full-screen container (backdrop + frame)
        private GameObject _frame;  // the cloned window frame
        private Transform _grid;    // GridLayoutGroup holding the slot cells
        private PickMode _mode;
        private bool _busy;         // a save/delete is in flight — block re-clicks until the refresh lands
        private int _generation;    // bumped every Close(); async callbacks captured from an older session no-op

        // Native-row reuse: when the game's RevertArchive prefab is loadable in-world we clone its native
        // 回溯列表行 (RevertArchiveItem: 头像/名字/年世/存档时间/地点排一行, 自带 _hovor 悬停高亮) for each
        // slot so the picker matches the game's own 回溯界面 (list-style SL). Fall back to BuildCellManual
        // if the prefab can't be obtained.
        private bool _nativeCells;          // this session builds native RevertArchiveItem list rows
        private int _cols = Columns;        // grid columns actually used (native list = 1)
        private Vector2 _cellSize;          // grid cell size actually used (native = row size)
        private static bool _templateFailLogged;     // 只记一次失败日志，避免每次开都刷屏
        private static GameObject _itemTemplate;     // 回溯列表行(RevertArchiveItem)的 srcPrefab，克隆每格（成功即缓存）

        // 分页：每页固定 5 个存档位(与 EqslSettings 同一常量，避免两处分叉)。
        private const int SlotsPerPage = EqslSettings.SlotsPerPage;
        // 当前页(0 基)——静态，跨"开/关选择器"与存/读两模式记忆页码(同一游戏进程内)；换页超范围时 RenderPage 会夹取。
        private static int _page;
        private SerializableModData _lastRes;    // 最近一次拉取的槽数据；翻页时免重拉直接重渲染
        private CButton _prevBtn, _nextBtn;      // 翻页按钮(RenderPage 内更新可点态)
        private GameObject _pageNav, _pageLabel;  // 翻页条容器 + 页码标签

        // SystemOption-root siblings hidden while the modal is up (menu frame, DLC banner, bottom bar, our
        // quick panel) so nothing draws over the picker; restored on Close.
        private readonly System.Collections.Generic.List<GameObject> _hidden =
            new System.Collections.Generic.List<GameObject>();

        // Layout (标准 galgame 存档位) — 每行两个；每格横向 = [序号盒:带边框无底色·满高] + [信息按钮:唯一可点]
        //   + [删除:最右竖直居中]。信息按钮内: 标题(备注/无则名字·年月) 左上 + 改名 紧跟标题右侧
        //   + 名字·年月 左下小字(仅当有用户备注) + 保存时间 右下小字。序号盒最新档用强调色边框。
        private const float FrameMinWidth  = 760f;   // 窗口最小宽
        private const float FrameMaxWidth  = 1500f;  // 窗口最大宽(防超宽屏过宽)
        private const float FrameWidthFrac = 0.92f;  // 目标宽 = 画布宽 × 此，夹在[min,max]
        private const int   Columns        = 2;     // 每行两个
        private const float CellH          = 84f;
        private const float CellSpacingX   = 12f;
        private const float CellSpacingY   = 10f;
        private const float SeqW           = 50f;   // 左侧序号盒宽度
        // 改名/删除 图标按钮尺寸在 BuildCell 内按标题字号动态算(图标本体≈存档名字号)。
        // 字号统一以【游戏原生按钮字号】为基准(BaseFont)乘系数，保证与游戏一致。
        private const float SeqFontFactor   = 1.25f; // 序号 01/02
        private const float TitleFontFactor = 1.1f;  // 标题(备注 / 名字·年月)：显著大于小字，且贴合游戏
        private const float SmallFontFactor = 0.62f; // 名字·年月 / 时间 小字(两列信息区窄，压小以防左右相撞)
        private static readonly UnityEngine.Color BorderColor = new UnityEngine.Color(1f, 1f, 1f, 0.35f);
        private static readonly UnityEngine.Color LatestColor = new UnityEngine.Color(1f, 0.84f, 0.4f, 0.95f);
        private const float TitleInset     = 84f;   // frame top reserved for the title
        private const float CloseInset     = 84f;   // frame bottom reserved for the 返回 button
        private const float MaxFrameHeight = 1080f; // cap so a large SlotCount stays on-screen (beyond it, the grid scrolls)

        public bool IsOpen => _root != null;

        public void SetTemplates(GameObject frameTemplate, CButton buttonTemplate, Transform canvasRoot)
        {
            _frameTemplate = frameTemplate;
            _buttonTemplate = buttonTemplate;
            _canvasRoot = canvasRoot;
        }

        public void Open(PickMode mode)
        {
            Close();
            _mode = mode;
            try
            {
                if (!Build()) { Close(); return; }
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyQuickSaveLoad] SlotPicker open failed: " + ex);
                Close();
            }
        }

        public void Close()
        {
            _generation++; // invalidate any in-flight refresh/poll callbacks from this session
            RestoreOverlappingElements();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _frame = null;
            _grid = null;
            _confirm = null;
            _busy = false;
            // 注意：_page 静态、故意不在 Close 里清零——跨开关记忆页码。
            _lastRes = null;
            _prevBtn = _nextBtn = null;
            _pageNav = _pageLabel = null;
        }

        /// <summary>Hide everything of the SystemOption menu that would draw over the modal, then restore on
        /// Close. Two levels: _canvasRoot's children (the menu frame + our quick panel) and _canvasRoot's
        /// PARENT's children (the DLC banner, the bottom button bar, the tips line — siblings of ElementRoot).</summary>
        private void HideOverlappingElements()
        {
            _hidden.Clear();
            HideSiblingsExcept(_canvasRoot, _root);
            if (_canvasRoot.parent != null)
            {
                HideSiblingsExcept(_canvasRoot.parent, _canvasRoot.gameObject);
            }
        }

        private void HideSiblingsExcept(Transform parent, GameObject keep)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).gameObject;
                if (child == keep) continue;
                if (child.activeSelf)
                {
                    child.SetActive(false);
                    _hidden.Add(child);
                }
            }
        }

        private void RestoreOverlappingElements()
        {
            foreach (var go in _hidden)
            {
                if (go != null) go.SetActive(true);
            }
            _hidden.Clear();
        }

        private bool Build()
        {
            if (_frameTemplate == null || _buttonTemplate == null || _canvasRoot == null) return false;

            // Full-screen container parented under the SystemOption root (an INTERACTIVE layer with a
            // GraphicRaycaster) so its buttons actually receive clicks — LayerVeryTop is display-only (no
            // raycaster) and silently swallows input. Last sibling so it draws above the native menu; the
            // overlapping decorations (DLC banner, bottom bar) are hidden separately while the modal is up.
            _root = new GameObject("EQSL_SlotPicker", typeof(RectTransform));
            var rrt = (RectTransform)_root.transform;
            rrt.SetParent(_canvasRoot, false);
            Stretch(rrt);
            _root.transform.SetAsLastSibling();

            // Dimmed backdrop that closes the picker on an outside click.
            var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            Stretch((RectTransform)bg.transform);
            bg.transform.SetParent(_root.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            bg.GetComponent<Button>().onClick.AddListener(Close);

            // Centered cloned frame.
            _frame = UnityEngine.Object.Instantiate(_frameTemplate, _root.transform);
            _frame.name = "EQSL_SlotFrame";
            _frame.SetActive(true); // template may be hidden (see HideOverlappingElements) — force the clone active
            var frt = (RectTransform)_frame.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            float canvasW = ((RectTransform)_canvasRoot).rect.width;
            // Decide native-vs-manual cells + the grid geometry (_cols/_cellSize) before sizing the frame.
            _itemTemplate = GetItemTemplate();
            _nativeCells = _itemTemplate != null;
            ComputeLayout(canvasW);

            int count = Mathf.Max(1, EqslSettings.SlotCount);
            float wantH = FrameHeightForCount(count);
            // Never let the frame exceed the canvas (title/返回 would clip off-screen on a short window). The
            // canvas root (_canvasRoot) is full-screen, so its rect height is the available canvas height.
            float canvasH = ((RectTransform)_canvasRoot).rect.height;
            if (canvasH > 200f) wantH = Mathf.Min(wantH, canvasH - 120f);
            // 宽度：原生卡按卡宽×列数定框宽(不拉伸卡片)；手搓格按画布自适应(×FrameWidthFrac 夹在[min,max])。
            float wantW;
            if (_nativeCells)
                wantW = _cols * _cellSize.x + (_cols - 1) * CellSpacingX + 48f + 24f; // grid inset(48) + 余量
            else
                wantW = canvasW > 400f ? Mathf.Clamp(canvasW * FrameWidthFrac, FrameMinWidth, FrameMaxWidth) : FrameMinWidth;
            if (canvasW > 400f) wantW = Mathf.Min(wantW, canvasW - 80f); // never wider than the canvas
            frt.sizeDelta = new Vector2(wantW, wantH);

            var title = _frame.transform.Find("Title");
            if (title != null)
                SystemOptionButtonInjector.SetLabelViaReflection(title.gameObject,
                    _mode == PickMode.Save ? "存档 · 选择栏位" : "读档 · 选择栏位");

            // Strip every frame child except the title; we build our own body (grid + close).
            for (int i = _frame.transform.childCount - 1; i >= 0; i--)
            {
                var child = _frame.transform.GetChild(i);
                if (child == title) continue;
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            BuildGrid();
            BuildPageNav();

            // Hide the menu + decorations LAST — after the frame was cloned from the (still-active)
            // MainWindow template. Doing it earlier would clone an inactive template into an invisible frame.
            HideOverlappingElements();
            return true;
        }

        private void BuildGrid()
        {
            // A ScrollRect fills the body (between title and close), so a large SlotCount scrolls and is
            // clipped by a RectMask2D instead of overflowing the frame onto the 返回 button.
            var scrollGO = new GameObject("EQSL_Scroll", typeof(RectTransform), typeof(ScrollRect));
            var srt = (RectTransform)scrollGO.transform;
            srt.SetParent(_frame.transform, false);
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            // 已无「返回」；分页(SlotCount>5)时底部给翻页条留高，否则很小的底边距。
            float bottomInset = EqslSettings.SlotCount > SlotsPerPage ? 76f : 26f;
            srt.offsetMin = new Vector2(24f, bottomInset); // left, bottom
            srt.offsetMax = new Vector2(-24f, -80f);       // right, top (leave room below the title)
            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var vprt = (RectTransform)viewportGO.transform;
            vprt.SetParent(scrollGO.transform, false);
            vprt.anchorMin = Vector2.zero;
            vprt.anchorMax = Vector2.one;
            vprt.pivot = new Vector2(0f, 1f);
            vprt.offsetMin = Vector2.zero;
            vprt.offsetMax = Vector2.zero;
            // A near-invisible raycast target over the whole viewport so the mouse wheel scrolls even over
            // the gaps between cells (RectMask2D alone provides no raycast graphic).
            var vpImg = viewportGO.GetComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.004f);
            vpImg.raycastTarget = true;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var crt = (RectTransform)contentGO.transform;
            crt.SetParent(viewportGO.transform, false);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var glg = contentGO.GetComponent<GridLayoutGroup>();
            if (_nativeCells)
            {
                glg.cellSize = _cellSize; // 原生存档卡按其原始尺寸，不拉伸
            }
            else
            {
                // 格宽随窗口自适应：_cols 列刚好铺满网格宽(scroll 左右各内缩 24 → 共 48)。
                float gridW = ((RectTransform)_frame.transform).rect.width - 48f;
                float cellW = (gridW - (_cols - 1) * CellSpacingX) / _cols;
                if (cellW < 240f) cellW = 240f;
                glg.cellSize = new Vector2(cellW, CellH);
            }
            glg.spacing = new Vector2(CellSpacingX, CellSpacingY);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = _cols;
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.padding = new RectOffset(0, 0, 0, 6);
            var csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vprt;
            scroll.content = crt;
            _grid = contentGO.transform;
        }

        /// <summary>翻页条(仅 SlotCount>5 时)：[上一页] 第X/Y页 [下一页]，位于框底居中。只建一次；
        /// 页码/按钮可点态在 <see cref="UpdatePageNav"/> 里随 RenderPage 更新。</summary>
        private void BuildPageNav()
        {
            if (EqslSettings.SlotCount <= SlotsPerPage) return; // 单页无需翻页

            _pageNav = new GameObject("EQSL_PageNav", typeof(RectTransform));
            var nrt = (RectTransform)_pageNav.transform;
            nrt.SetParent(_frame.transform, false);
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.anchoredPosition = new Vector2(0f, 20f); // 框底居中(已无「返回」)
            nrt.sizeDelta = new Vector2(440f, 46f);

            _prevBtn = CloneButton("上一页", () => { if (_page > 0) { _page--; RenderPage(); } });
            var prt = (RectTransform)_prevBtn.transform;
            prt.SetParent(_pageNav.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(120f, 44f);

            _pageLabel = MakeLabel("第 1 / 1 页", BaseFont() * 0.82f);
            SetAlignment(_pageLabel, "Center", "MiddleCenter");
            var lrt = (RectTransform)_pageLabel.transform;
            lrt.SetParent(_pageNav.transform, false);
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(170f, 40f);

            _nextBtn = CloneButton("下一页", () => { _page++; RenderPage(); }); // RenderPage 会夹取页码
            var xrt = (RectTransform)_nextBtn.transform;
            xrt.SetParent(_pageNav.transform, false);
            xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 0.5f);
            xrt.pivot = new Vector2(1f, 0.5f);
            xrt.anchoredPosition = Vector2.zero;
            xrt.sizeDelta = new Vector2(120f, 44f);
        }

        private void UpdatePageNav(int pages)
        {
            if (_pageNav == null) return;
            _pageNav.SetActive(pages > 1);
            if (_pageLabel != null)
                SystemOptionButtonInjector.SetLabelViaReflection(_pageLabel, "第 " + (_page + 1) + " / " + pages + " 页");
            if (_prevBtn != null) _prevBtn.interactable = _page > 0;
            if (_nextBtn != null) _nextBtn.interactable = _page < pages - 1;
        }

        private void Refresh()
        {
            int gen = _generation;
            EqslCore.RequestSlots(res =>
            {
                if (gen != _generation || _grid == null) return; // stale session / closed
                try { BuildCells(res); }
                catch (Exception ex) { Debug.LogError("[EasyQuickSaveLoad] SlotPicker refresh failed: " + ex); }
            });
        }

        /// <summary>Refresh after a delay, letting an async note write land before re-reading slots.</summary>
        private void RefreshDelayed(float sec)
        {
            int gen = _generation;
            try { SingletonObject.getInstance<YieldHelper>().DelaySecondsDo(sec, () => { if (gen == _generation) Refresh(); }); }
            catch { Refresh(); }
        }

        /// <summary>
        /// After a save/delete (fire-and-forget, and the backend write can take ~1s), poll ListSlots until the
        /// target slot reaches the expected state, then rebuild + clear the re-entry guard. For a SAVE we wait
        /// for a strictly-newer timestamp than the pre-save one — an overwrite leaves the slot 'filled' the
        /// whole time, so an existence-only check would settle before the deferred write actually lands.
        /// The generation token makes a callback from a since-closed session a no-op.
        /// </summary>
        private void PollRefresh(int slot, bool wantFilled, long preSaveTicks, int attemptsLeft, int gen)
        {
            EqslCore.RequestSlots(res =>
            {
                if (gen != _generation || _grid == null) return; // stale/closed — the live session owns _busy
                ArchiveInfo si = null;
                res?.Get("SlotInfos", out si);
                var wi = SlotInfoAt(si, slot);
                bool settled = wantFilled ? (wi != null && wi.SavingTimestamp > preSaveTicks) : (wi == null);
                if (settled || attemptsLeft <= 0)
                {
                    try { BuildCells(res); }
                    catch (Exception ex) { Debug.LogError("[EasyQuickSaveLoad] SlotPicker poll-refresh failed: " + ex); }
                    _busy = false;
                    return;
                }
                try { SingletonObject.getInstance<YieldHelper>().DelaySecondsDo(0.4f, () => PollRefresh(slot, wantFilled, preSaveTicks, attemptsLeft - 1, gen)); }
                catch { _busy = false; Refresh(); }
            });
        }

        private float FrameHeightForCount(int count)
        {
            int rowsThisPage = Mathf.Min(count, SlotsPerPage);         // 分页：每屏最多 5 个
            int rows = Mathf.CeilToInt(rowsThisPage / (float)_cols);
            float cellH = _nativeCells ? _cellSize.y : CellH;
            float gridH = rows * cellH + Mathf.Max(0, rows - 1) * CellSpacingY;
            float bottomReserve = count > SlotsPerPage ? 76f : 26f;   // 翻页条(有则)+底边距；已无「返回」
            return Mathf.Min(MaxFrameHeight, TitleInset + gridH + bottomReserve);
        }

        // 取原生回溯列表行模板(仅一次)：世界内用 ResLoader 载 ViewRevertArchive 预制体，反射其私有 scroll
        // (InfinityScroll) 的 srcPrefab(= RevertArchiveItem)。取不到(路径/结构变动)则回退手搓格。
        // 完整路径 = rootPrefabPath + UIElement._path（见 UIElement.PrepareRes）。
        private const string RevertPrefabPath = "RemakeResources/Prefab/Views/RecordSelect/ViewRevertArchive";
        private static bool _asyncPreloadStarted;
        // 列表行较宽时的目标框宽下限(拉伸锚点预制体 sizeDelta≈0 时用)。
        private const float NativeRowWidth = 720f;

        private static GameObject GetItemTemplate()
        {
            if (_itemTemplate != null) return _itemTemplate; // 成功缓存；否则每次开都重试
            try
            {
                // UI 预制体在 AssetBundle 里，普通 Resources.Load 世界内取不到 → 用游戏自己的 ResLoader.SyncLoad
                // (从已加载资源包同步取，完整路径带 RemakeResources/Prefab/Views/ 前缀)。
                GameObject prefab = null;
                try { prefab = ResLoader.SyncLoad<GameObject>(RevertPrefabPath); } catch { }
                if (prefab == null) prefab = Resources.Load<GameObject>(RevertPrefabPath); // 兜底
                if (prefab == null)
                {
                    // 同步没取到 → 用游戏自身的异步加载器预载(填静态缓存)，下次开即原生行，无需重启。
                    KickOffAsyncPreload();
                    if (!_templateFailLogged) { Debug.LogWarning("[EasyQuickSaveLoad] 回溯列表模板同步未取到，已发起异步预载(下次开生效)，本次回退手搓格。"); _templateFailLogged = true; }
                    return null;
                }
                ExtractTemplate(prefab);
                if (_itemTemplate == null && !_templateFailLogged)
                { Debug.LogWarning("[EasyQuickSaveLoad] 取 RevertArchiveItem srcPrefab 失败，存档位回退手搓格。"); _templateFailLogged = true; }
            }
            catch (Exception ex)
            {
                if (!_templateFailLogged) { Debug.LogError("[EasyQuickSaveLoad] GetItemTemplate 异常，回退手搓格: " + ex); _templateFailLogged = true; }
                _itemTemplate = null;
            }
            return _itemTemplate;
        }

        /// <summary>从 ViewRevertArchive 预制体反射出私有 scroll(InfinityScroll)→ 其 public srcPrefab
        /// (= RevertArchiveItem 列表行预制体) 存入静态缓存。</summary>
        private static void ExtractTemplate(GameObject prefab)
        {
            var vra = prefab.GetComponent<ViewRevertArchive>();
            var scrollField = typeof(ViewRevertArchive).GetField("scroll", BindingFlags.Instance | BindingFlags.NonPublic);
            var scroll = scrollField != null ? scrollField.GetValue(vra) : null;
            if (scroll == null) return;
            var srcField = scroll.GetType().GetField("srcPrefab", BindingFlags.Instance | BindingFlags.Public);
            _itemTemplate = srcField != null ? srcField.GetValue(scroll) as GameObject : null;
            if (_itemTemplate != null) Debug.Log("[EasyQuickSaveLoad] 已取得回溯列表行模板，存档位使用原生列表行。");
        }

        /// <summary>用游戏自身的异步加载器(ResLoader.Load，与 UIElement.PrepareRes 同款)预载回溯面板预制体，
        /// 完成后填缓存——供下次开选择器使用(本次已回退手搓，无需重启)。只发起一次。</summary>
        private static void KickOffAsyncPreload()
        {
            if (_asyncPreloadStarted) return;
            _asyncPreloadStarted = true;
            try { ResLoader.Load<GameObject>(RevertPrefabPath, go => { if (go != null) ExtractTemplate(go); }, null, false); }
            catch { }
        }

        /// <summary>定网格几何：原生列表行=单列、按行原始高(宽拉伸到框宽)；手搓格用固定两列 + 自适应格宽。</summary>
        private void ComputeLayout(float canvasW)
        {
            if (_nativeCells)
            {
                Vector2 ns = ((RectTransform)_itemTemplate.transform).sizeDelta;
                float rowW = ns.x > 60f ? ns.x : NativeRowWidth; // 拉伸锚点(sizeDelta≈0)时退回估值
                float rowH = ns.y > 20f ? ns.y : 96f;
                if (canvasW > 400f) rowW = Mathf.Min(Mathf.Max(rowW, NativeRowWidth), canvasW - 120f);
                _cols = 1;                                        // 列表式：一行一档
                _cellSize = new Vector2(rowW, rowH);
            }
            else
            {
                _cols = Columns;
                _cellSize = new Vector2(0f, CellH); // 手搓格宽在 BuildGrid 内按框宽算
            }
        }

        private void BuildCells(SerializableModData res)
        {
            _lastRes = res;   // 缓存供翻页时免重拉直接重渲染
            RenderPage();
        }

        /// <summary>按当前页(_page)渲染最多 5 个存档位；数据取自缓存的 _lastRes。翻页/刷新都走这里。</summary>
        private void RenderPage()
        {
            if (_grid == null) return;

            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var c = _grid.GetChild(i);
                c.SetParent(null, false);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            int count = EqslSettings.SlotCount;
            ArchiveInfo slotInfos = null;
            if (_lastRes != null)
            {
                _lastRes.Get("SlotInfos", out slotInfos);
                _lastRes.Get("SlotCount", out count);
            }
            count = Mathf.Clamp(count, 1, 99); // guard against an unclamped backend/config value
            var notes = EqslCore.ParseNotes(_lastRes);

            // 【最新】：全局(跨页)找最近写入的已填槽。
            int latest = -1;
            long latestTs = long.MinValue;
            for (int slot = 0; slot < count; slot++)
            {
                var wi = SlotInfoAt(slotInfos, slot);
                if (wi != null && wi.SavingTimestamp > latestTs) { latestTs = wi.SavingTimestamp; latest = slot; }
            }

            int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)SlotsPerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            int from = _page * SlotsPerPage;
            int to = Mathf.Min(from + SlotsPerPage, count);
            for (int slot = from; slot < to; slot++)
            {
                notes.TryGetValue(slot, out string note);
                BuildCell(slot, SlotInfoAt(slotInfos, slot), note, slot == latest);
            }

            UpdatePageNav(pages);
        }

        private static WorldInfo SlotInfoAt(ArchiveInfo slotInfos, int slot)
        {
            var list = slotInfos?.BackupWorldsInfo;
            if (list == null) return null;
            foreach (var entry in list)
            {
                if (entry.Item1 == slot) return entry.Item2;
            }
            return null;
        }

        private void BuildCell(int slot, WorldInfo wi, string note, bool isLatest)
        {
            if (_nativeCells)
            {
                try { BuildCellNative(slot, wi, note, isLatest); return; }
                catch (Exception ex)
                {
                    // A native-row failure shouldn't blank the whole picker — degrade this cell to manual.
                    Debug.LogError("[EasyQuickSaveLoad] 原生列表行构建失败，该格回退手搓: " + ex);
                }
            }
            BuildCellManual(slot, wi, note, isLatest);
        }

        /// <summary>原生回溯列表行：克隆 RevertArchiveItem，用我们的槽数据 SetText+头像填充；保留原生
        /// 悬停高亮(不禁用 toggle)；点击整行=存/读(PointClickBridge)；行右自绘 改名+删除 小图标(列表行本无此二钮)。</summary>
        private void BuildCellNative(int slot, WorldInfo wi, string note, bool isLatest)
        {
            var go = UnityEngine.Object.Instantiate(_itemTemplate, _grid);
            try
            {
                go.name = "EQSL_RevertRow";
                go.SetActive(true); // 模板在预制体内可能是 inactive
                var item = go.GetComponent<RevertArchiveItem>();
                if (item == null) throw new Exception("克隆体缺少 RevertArchiveItem 组件");

                // 整行点击桥：优先反射 _pointClickBridge，但该字段在预制体里未赋值(为 null)，故回退到行根上
                // 实际挂着的 PointClickBridge 组件。之前 bridge 为 null → 点击走了 toggle 的 InitToggleListener 回退，
                // 导致选中态残留且我们的复位(以 bridge!=null 为条件)从不执行。
                var bridge = GetPrivateBridge(item) ?? go.GetComponent<PointClickBridge>();

                bool filled = wi != null;
                if (filled)
                {
                    string charName = SafeCharName(wi);
                    string display = string.IsNullOrEmpty(note) ? charName : note;
                    // SetText(名字, 年世, 存档时间, 州, 地区) —— 内部把 州-地区 拼成一行。
                    item.SetText(display, EqslCore.FormatYear(wi), EqslCore.FormatSaveTime(wi.SavingTimestamp),
                        EqslCore.FormatState(wi), EqslCore.FormatArea(wi));

                    // 头像(照原生 OnArchiveItemRender：先对齐 ClothDisplayId 再 Refresh)。
                    try
                    {
                        var ard = wi.AvatarRelatedData;
                        if (ard != null && ard.AvatarData != null)
                        {
                            try { ard.AvatarData.ClothDisplayId = ard.ClothingDisplayId; } catch { }
                            item.Avatar.Refresh(ard.AvatarData, ard.DisplayAge);
                        }
                        else item.Avatar.ResetToBlank();
                    }
                    catch { try { item.Avatar.ResetToBlank(); } catch { } }

                    // 改名(紧跟备注名) + 删除(整行最右) 小图标。
                    AddRowIcons(go, item, slot, wi, note);

                    // 整行主点击 = 存/读。清掉原生双击(否则单击被延迟 0.3s 消歧)。
                    if (bridge != null) { bridge.OnLeftClick = () => OnSlotClicked(slot, wi, note); bridge.OnDoubleClick = null; }
                    else item.InitToggleListener(isOn => { if (isOn) OnSlotClicked(slot, wi, note); }); // 兜底走 toggle

                    if (isLatest) AddLatestBadge(go);
                }
                else
                {
                    item.SetText("空栏位", string.Empty, string.Empty, string.Empty, string.Empty);
                    try { item.Avatar.ResetToBlank(); } catch { }
                    // 存档模式点空行=直接存；读档模式空行无操作(OnSlotClicked wi==null 早退)。
                    if (bridge != null) { bridge.OnLeftClick = () => OnSlotClicked(slot, null, null); bridge.OnDoubleClick = null; }
                    else item.InitToggleListener(isOn => { if (isOn) OnSlotClicked(slot, null, null); });
                }
                // 选中高亮 = CToggle 的 graphic(由 isOn 驱动)。整行点击时 CToggle(组件序 4)先于 PointClickBridge
                // (组件序 6)收到点击并置 isOn=true，且我们没走原生"选中→确认"流程，故会残留(对话框取消后不消、多行累积)。
                // 原生清选中就是 CToggleGroup.DeSelect → 令 isOn=false(PlayEffect 随之清高亮)。我们不需要选中态
                // (点击走 PointClickBridge、悬停走独立的 PointerTrigger)→ 每次整行点击后按原生做法把 isOn 复位。
                var tg = go.GetComponent<CToggle>(); // 被点击/被桥报告 isOn 的正是行根上的这个 CToggle
                if (bridge != null && tg != null)
                {
                    tg.SetIsOnWithoutNotify(false);           // 起始清干净
                    var inner = bridge.OnLeftClick;           // = OnSlotClicked(...)
                    // 延后一帧复位 isOn(与原生 DeSelect 同法)，确保跑在整个点击事件(CToggle 置 true)之后。
                    bridge.OnLeftClick = () =>
                    {
                        inner?.Invoke();
                        var t = tg;
                        try { SingletonObject.getInstance<YieldHelper>().DelayFrameDo(1u, () => { if (t != null) t.isOn = false; }); }
                        catch { if (t != null) t.isOn = false; }
                    };
                }
            }
            catch
            {
                // 本行构建失败：先脱离网格再销毁半成品，避免与随后手搓回退格在同帧重复占位。
                if (go != null) { go.transform.SetParent(null, false); UnityEngine.Object.Destroy(go); }
                throw;
            }
        }

        private void BuildCellManual(int slot, WorldInfo wi, string note, bool isLatest)
        {
            bool filled = wi != null;
            bool hasNote = filled && !string.IsNullOrEmpty(note);
            // 改名/删除 图标按钮：图标本体≈标题(存档名)字号(用户要求"和存档名一样大")，加少量内边距。
            float iconSz = BaseFont() * TitleFontFactor + 6f;

            // 标准 galgame 存档位：[序号盒] + [信息按钮(唯一可点)] + [删除(最右)]。
            var cell = new GameObject("EQSL_SlotCell", typeof(RectTransform));
            ((RectTransform)cell.transform).SetParent(_grid, false);

            // 序号盒 —— 左侧、带边框无底色、满高；最新档用强调色边框。纯标签，不可点。
            var seq = new GameObject("EQSL_SeqBox", typeof(RectTransform));
            var srt = (RectTransform)seq.transform;
            srt.SetParent(cell.transform, false);
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(SeqW, -8f);
            srt.anchoredPosition = new Vector2(4f, 0f);
            AddRectBorder(srt, 1.5f, isLatest ? LatestColor : BorderColor);
            var seqLabel = MakeLabel((slot + 1).ToString("00"), BaseFont() * SeqFontFactor);
            var qlt = (RectTransform)seqLabel.transform;
            qlt.SetParent(seq.transform, false);
            Stretch(qlt);

            // 信息按钮 —— 唯一可点区。清空模板自带居中文本，改用子元素排版。
            // 标题=存档名字(备注)，无备注则「未命名存档」；年月+时间另在下方，详情走悬停 tooltip。
            string title = filled ? (hasNote ? note : "未命名存档") : "（空）";
            var main = CloneButton(string.Empty, () => OnSlotClicked(slot, wi, note));
            var mrt = (RectTransform)main.transform;
            mrt.SetParent(cell.transform, false);
            mrt.anchorMin = new Vector2(0f, 0f);
            mrt.anchorMax = new Vector2(1f, 1f);
            mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.offsetMin = new Vector2(SeqW + 12f, 4f);
            mrt.offsetMax = new Vector2(-(iconSz + 12f), -4f);
            main.interactable = _mode == PickMode.Save || filled;

            // 标题(备注 / 无备注则 游戏内年月) —— 左上，较大。右侧留给改名图标。同世界内太吾名冗余，不显示。
            var titleLabel = MakeLabel(title, BaseFont() * TitleFontFactor);
            SetAlignment(titleLabel, "Left", "MiddleLeft");
            var tlt = (RectTransform)titleLabel.transform;
            tlt.SetParent(main.transform, false);
            tlt.anchorMin = new Vector2(0f, 1f);
            tlt.anchorMax = new Vector2(1f, 1f);
            tlt.pivot = new Vector2(0f, 1f);
            tlt.sizeDelta = new Vector2(-(iconSz + 10f), 36f);
            tlt.anchoredPosition = new Vector2(8f, -2f);

            if (filled)
            {
                // 改名 —— 紧跟标题右侧的小图标(独立可点)。
                var ren = CloneButton("改名", () => OnRenameClicked(slot, wi, note));
                var rrt = (RectTransform)ren.transform;
                rrt.SetParent(main.transform, false);
                rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot = new Vector2(1f, 1f);
                rrt.sizeDelta = new Vector2(iconSz, iconSz);
                rrt.anchoredPosition = new Vector2(-4f, -4f);
                IconStyler.Apply(ren, IconKind.Rename);

                // 游戏内年月 —— 左下小字(与游戏顶栏一致的"第X年 Y月")。始终显示。
                var nameLabel = MakeLabel(EqslCore.FormatGameDate(wi), BaseFont() * SmallFontFactor);
                SetAlignment(nameLabel, "Left", "MiddleLeft");
                var nlt = (RectTransform)nameLabel.transform;
                nlt.SetParent(main.transform, false);
                nlt.anchorMin = nlt.anchorMax = new Vector2(0f, 0f);
                nlt.pivot = new Vector2(0f, 0f);
                nlt.sizeDelta = new Vector2(170f, 22f);
                nlt.anchoredPosition = new Vector2(8f, 6f);

                // 保存时间 —— 右下小字。
                var timeLabel = MakeLabel(EqslCore.FormatSaveTimeShort(wi.SavingTimestamp), BaseFont() * SmallFontFactor);
                SetAlignment(timeLabel, "Right", "MiddleRight");
                var tmt = (RectTransform)timeLabel.transform;
                tmt.SetParent(main.transform, false);
                tmt.anchorMin = tmt.anchorMax = new Vector2(1f, 0f);
                tmt.pivot = new Vector2(1f, 0f);
                tmt.sizeDelta = new Vector2(170f, 22f);
                tmt.anchoredPosition = new Vector2(-8f, 6f);

                // 悬停详情 tooltip —— 给整格挂原生 SingleDesc 提示：备注(有则) + 快速SL同款预览
                // (存档时间 / 太吾名·年世 / 地点)。triggerByChildRaycast=悬停格内任意子控件都触发。
                var tip = cell.AddComponent<TooltipInvoker>();
                tip.Type = TipType.SingleDesc;
                tip.triggerByChildRaycast = true;
                tip.RuntimeParam = EasyPool.Get<ArgumentBox>().Set("arg0", TooltipDetail(wi, note));
            }

            // 删除 —— 最右、竖直居中的小图标(独立可点)；空档禁用。
            var del = CloneButton("删除", () => OnDeleteClicked(slot, wi, note));
            var drt = (RectTransform)del.transform;
            drt.SetParent(cell.transform, false);
            drt.anchorMin = drt.anchorMax = new Vector2(1f, 0.5f);
            drt.pivot = new Vector2(1f, 0.5f);
            drt.sizeDelta = new Vector2(iconSz, iconSz);
            drt.anchoredPosition = new Vector2(-6f, 0f);
            del.interactable = filled;
            IconStyler.Apply(del, IconKind.Delete);
        }

        /// <summary>给一个 RectTransform 画"带边框无底色"的矩形：四条细边(Image)，不吃点击。</summary>
        private static void AddRectBorder(RectTransform parent, float thickness, UnityEngine.Color color)
        {
            AddEdge(parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness)); // top
            AddEdge(parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness)); // bottom
            AddEdge(parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f)); // left
            AddEdge(parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f)); // right
        }

        private static void AddEdge(RectTransform parent, UnityEngine.Color color, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject("EQSL_Border", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>纯文本标签原语：克隆按钮模板 → 关交互 + 隐藏底图/边框(Image/CImage) + 去 raycast，
        /// 只留文字组件(继承游戏字体)。用于编号/时间这类不可点角标。</summary>
        private GameObject MakeLabel(string text, float fontSize)
        {
            var go = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject);
            go.name = "EQSL_Label";
            go.SetActive(true);
            SystemOptionButtonInjector.SetLabelViaReflection(go, text);
            SetFontSize(go, fontSize);
            var btn = go.GetComponent<CButton>();
            if (btn != null) btn.interactable = false;
            foreach (var g in go.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                if (g.GetType().Name.Contains("Image")) g.enabled = false; // 隐藏按钮底图/边框，只留文字
                g.raycastTarget = false;                                    // 标签不吃点击
            }
            return go;
        }

        /// <summary>把克隆按钮/标签上第一个可写 fontSize(UI.Text=int / TMP=float) 设为指定字号(反射，跨类型)。</summary>
        private static void SetFontSize(GameObject go, float size)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var p = c.GetType().GetProperty("fontSize",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (p == null || !p.CanWrite) continue;
                try { p.SetValue(c, System.Convert.ChangeType(size, p.PropertyType), null); return; }
                catch { }
            }
        }

        /// <summary>把第一个可写 alignment(枚举)设为首个能解析的候选名(TMP=TextAlignmentOptions 用"Left"/"Right"…；
        /// UI.Text=TextAnchor 用"MiddleLeft"/"MiddleRight"…)。跨类型，故传多个候选名。</summary>
        private static void SetAlignment(GameObject go, params string[] enumNames)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var p = c.GetType().GetProperty("alignment",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (p == null || !p.CanWrite || !p.PropertyType.IsEnum) continue;
                foreach (var name in enumNames)
                {
                    try { p.SetValue(c, System.Enum.Parse(p.PropertyType, name), null); return; }
                    catch { }
                }
            }
        }

        /// <summary>读取克隆体上第一个可读 fontSize(游戏原生字号)。</summary>
        private static float ReadFontSize(GameObject go, float fallback)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var p = c.GetType().GetProperty("fontSize",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (p == null || !p.CanRead) continue;
                try { float f = System.Convert.ToSingle(p.GetValue(c, null)); if (f > 0f) return f; }
                catch { }
            }
            return fallback;
        }

        private float _baseFont;
        /// <summary>游戏原生按钮字号(缓存)，作为存档位各字号的统一基准。</summary>
        private float BaseFont()
        {
            if (_baseFont <= 0f) _baseFont = ReadFontSize(_buttonTemplate.gameObject, 30f);
            return _baseFont;
        }

        /// <summary>太吾名(用游戏原生逻辑，含隐姓设置)；异常时退回我们的格式化，再退回空串。</summary>
        private static string SafeCharName(WorldInfo wi)
        {
            try { return ViewRecordSelect.GetCharacterName(wi); }
            catch { try { return EqslCore.FormatTaiwuName(wi); } catch { return string.Empty; } }
        }

        /// <summary>反射取 RevertArchiveItem 的私有 PointClickBridge(_pointClickBridge，无公开 getter)。</summary>
        private static PointClickBridge GetPrivateBridge(RevertArchiveItem item)
        {
            try
            {
                var f = typeof(RevertArchiveItem).GetField("_pointClickBridge", BindingFlags.Instance | BindingFlags.NonPublic);
                return f != null ? f.GetValue(item) as PointClickBridge : null;
            }
            catch { return null; }
        }

        /// <summary>最新档角标：左上小字「★最新」(强调色)。</summary>
        private void AddLatestBadge(GameObject cell)
        {
            var badge = MakeLabel("★最新", BaseFont() * 0.6f);
            SetAlignment(badge, "Left", "MiddleLeft");
            SetTextColor(badge, LatestColor);
            var rt = (RectTransform)badge.transform;
            rt.SetParent(cell.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(130f, 26f);
            rt.anchoredPosition = new Vector2(10f, -4f);
            rt.SetAsLastSibling();
        }

        /// <summary>列表行图标(回溯行本身无此二钮)：删除放整行最右；改名紧跟备注名右侧(挂到 _name 元素右边)，
        /// 不放整行末尾。子按钮消费点击，不会误触整行的存/读。</summary>
        private void AddRowIcons(GameObject row, RevertArchiveItem item, int slot, WorldInfo wi, string note)
        {
            var nameRt = GetPrivateRect(item, "_name");
            // 名字文字原生是"水平居中+竖直顶对齐"，导致文字骑在竖直居中的改名笔上方 → 改为竖直居中，
            // 二者(及删除笔)对齐。SetAlignment("Center")=水平居中+竖直居中，水平不变。
            if (nameRt != null) SetAlignment(nameRt.gameObject, "Center", "MiddleCenter");
            // 图标尺寸=名字字号的约2倍(用户要求)，对齐模组管理里那类图标按钮的尺度(实测其加载顺序编辑格
            // 约 52、上下箭头 44×26 画布单位)；夹在 [44,56] 防极端。
            float nameFont = nameRt != null ? ReadFontSize(nameRt.gameObject, BaseFont()) : BaseFont();
            float sz = Mathf.Clamp(nameFont * 2f, 44f, 56f);
            // 删除：整行最右、竖直居中。
            var del = CloneButton("删除", () => OnDeleteClicked(slot, wi, note));
            PlaceRowIcon(del, row, sz, -12f);
            IconStyler.Apply(del, IconKind.Delete);
            // 改名：紧跟备注名右侧。
            var ren = CloneButton("改名", () => OnRenameClicked(slot, wi, note));
            IconStyler.Apply(ren, IconKind.Rename);
            var rt = (RectTransform)ren.transform;
            rt.sizeDelta = new Vector2(sz, sz);
            if (nameRt != null)
            {
                rt.SetParent(nameRt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);          // 以右边界为基准 → 对齐名字列右分隔线(留 margin)
                rt.anchoredPosition = new Vector2(20f, 0f); // 名字元素右缘→分隔线约 29px，右边界落在分隔线内侧 ~9px
                rt.SetAsLastSibling();
            }
            else PlaceRowIcon(ren, row, sz, -(12f + sz + 10f)); // 兜底：取不到名字元素→放最右次位
        }

        private static void PlaceRowIcon(CButton btn, GameObject row, float sz, float xFromRight)
        {
            var rt = (RectTransform)btn.transform;
            rt.SetParent(row.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(sz, sz);
            rt.anchoredPosition = new Vector2(xFromRight, 0f);
            rt.SetAsLastSibling();
        }

        /// <summary>反射取 RevertArchiveItem 私有字段的 RectTransform(如 _name)，不引用 TMP 类型。</summary>
        private static RectTransform GetPrivateRect(RevertArchiveItem item, string field)
        {
            try
            {
                var f = typeof(RevertArchiveItem).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                var comp = f != null ? f.GetValue(item) as Component : null;
                return comp != null ? comp.transform as RectTransform : null;
            }
            catch { return null; }
        }

        /// <summary>把标签的文字组件(有 text 属性者)的 color 设为指定色(反射，跨 TMP/UI.Text)。</summary>
        private static void SetTextColor(GameObject go, UnityEngine.Color color)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var t = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                var p = c.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
                if (t == null || p == null || !p.CanWrite || p.PropertyType != typeof(UnityEngine.Color)) continue;
                try { p.SetValue(c, color, null); return; }
                catch { }
            }
        }

        private void OnRenameClicked(int slot, WorldInfo wi, string note)
        {
            if (wi == null) return;
            // 预填与列表显示一致：有备注→备注；无备注→太吾名(列表无备注时正是显示太吾名)。之前预填游戏日期
            // 与显示的太吾名不符，令人困惑。
            string prefill = string.IsNullOrEmpty(note) ? SafeCharName(wi) : note;
            NativeRename.Prompt("备注名", prefill, 16, s =>
            {
                EqslCore.CallSetNote(slot, s);
                RefreshDelayed(0.35f);
            });
        }

        private void OnSlotClicked(int slot, WorldInfo wi, string note)
        {
            if (_busy) return;                              // a save/delete is still settling — ignore re-clicks
            if (!EqslCore.CanOperate(out _)) return;        // re-gate: state may have changed since the picker opened

            if (_mode == PickMode.Save)
            {
                Action doSave = () =>
                {
                    // 注意：不再 re-check CanOperate——原生 Dialog 打开时 CanOperate 恒为 false(界面中/BlockHotKey)，
                    // 会误吞保存。入口已 gate 过、模态期间月/事件也不会触发，故只挡重入 _busy 即可。
                    if (_busy) return;
                    _busy = true;
                    long preTicks = wi != null ? wi.SavingTimestamp : long.MinValue; // wait for a newer save than this
                    EqslCore.CallSave(slot);
                    PollRefresh(slot, wantFilled: true, preSaveTicks: preTicks, attemptsLeft: 8, gen: _generation);
                };
                if (wi != null && EqslSettings.OverwriteConfirm)
                    NativeDialog.Confirm("覆盖存档", "将覆盖此栏位已有的存档", doSave);
                else
                    doSave(); // 空栏或关闭「覆盖确认」→ 直接存
            }
            else
            {
                if (wi == null) return;
                NativeDialog.Confirm("读取存档", "将读取此栏位存档并进入", () =>
                {
                    if (_busy) return; // 同上，勿 re-check CanOperate
                    Close();
                    HideSystemOption();
                    EqslCore.LoadSlotWorld(slot);
                });
            }
        }

        private void OnDeleteClicked(int slot, WorldInfo wi, string note)
        {
            if (_busy) return;
            Action doDelete = () =>
            {
                if (_busy) return; // 同上，勿 re-check CanOperate
                _busy = true;
                EqslCore.CallDelete(slot);
                PollRefresh(slot, wantFilled: false, preSaveTicks: 0L, attemptsLeft: 8, gen: _generation);
            };
            if (EqslSettings.DeleteConfirm)
                NativeDialog.Confirm("删除存档", "将删除此栏位存档", doDelete);
            else
                doDelete(); // 关闭「删除确认」→ 直接删
        }

        // ---- In-picker confirm (a child of _root, so it always renders above the grid regardless of
        //      what UI layer the picker sits on — the native Dialog would be below the LayerVeryTop modal). ----

        private GameObject _confirm;

        private void ShowConfirm(string title, string body, Action onYes)
        {
            if (_root == null) return;
            CloseConfirm();

            _confirm = new GameObject("EQSL_Confirm", typeof(RectTransform), typeof(Image), typeof(Button));
            var crt = (RectTransform)_confirm.transform;
            crt.SetParent(_root.transform, false);
            Stretch(crt);
            _confirm.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            _confirm.GetComponent<Button>().onClick.AddListener(CloseConfirm); // 点外部=取消
            _confirm.transform.SetAsLastSibling();

            var f = UnityEngine.Object.Instantiate(_frameTemplate, _confirm.transform);
            f.name = "EQSL_ConfirmFrame";
            f.SetActive(true); // template may be hidden while the modal is up — force the clone active
            var frt = (RectTransform)f.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(440f, 240f); // 内容已精简 → 紧凑

            // 标题：保留原生标题栏(红条背景)但清空其自带文字；先清掉其余原生子物件，再叠自绘标题。
            // (顺序很重要——自绘标题必须在清子物件之后加，否则会被这轮清理连带删掉。)
            var titleTf = f.transform.Find("Title");
            if (titleTf != null) SystemOptionButtonInjector.SetLabelViaReflection(titleTf.gameObject, string.Empty);
            for (int i = f.transform.childCount - 1; i >= 0; i--)
            {
                var c = f.transform.GetChild(i);
                if (c == titleTf) continue;
                c.SetParent(null, false);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            // 自绘标题：左对齐 + 左内边距。直接左对齐原生标题文字会顶出标题栏左边界(它是为宽菜单设计的、
            // 文字框比可视红条更靠左)，故不用它、自绘一个位置完全可控的标题。
            var titleLabel = MakeLabel(title, BaseFont() * 0.82f);
            SetAlignment(titleLabel, "Left", "MiddleLeft");
            var ttrt = (RectTransform)titleLabel.transform;
            ttrt.SetParent(f.transform, false);
            ttrt.anchorMin = ttrt.anchorMax = new Vector2(0f, 1f); // 框左上角
            ttrt.pivot = new Vector2(0f, 1f);
            ttrt.sizeDelta = new Vector2(320f, 46f);
            ttrt.anchoredPosition = new Vector2(34f, -22f); // 左内边距 34，下移到标题栏中部
            ttrt.SetAsLastSibling();

            // 正文：标题下方居中的单行确认句(存档详情已在选择器界面展示，无需重复)。
            var bodyGo = MakeLabel(body, BaseFont() * 0.72f);
            SetAlignment(bodyGo, "Center", "MiddleCenter");
            var brt = (RectTransform)bodyGo.transform;
            brt.SetParent(f.transform, false);
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(400f, 60f);
            brt.anchoredPosition = new Vector2(0f, 8f);

            var yes = CloneButton("确认", () => { CloseConfirm(); onYes?.Invoke(); });
            PlaceConfirmButton(yes, f.transform, -90f);
            var no = CloneButton("取消", CloseConfirm);
            PlaceConfirmButton(no, f.transform, 90f);
        }

        /// <summary>悬停 tooltip 详情：备注(有则)换行 + 快速SL同款存档预览(存档时间 / 太吾名·年世 / 地点)。</summary>
        private static string TooltipDetail(WorldInfo wi, string note)
        {
            string s = string.IsNullOrEmpty(note) ? "" : (note + "\n");
            return s + EqslCore.FormatWorldInfoBrief(wi);
        }

        private static void PlaceConfirmButton(CButton btn, Transform parent, float x)
        {
            var rt = (RectTransform)btn.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -66f);
            rt.sizeDelta = new Vector2(150f, 46f);
        }

        private void CloseConfirm()
        {
            if (_confirm != null) UnityEngine.Object.Destroy(_confirm);
            _confirm = null;
        }

        private CButton CloneButton(string label, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject);
            go.name = "EQSL_SlotBtn";
            go.SetActive(true); // template's ancestor may be hidden — ensure the clone itself is active
            SystemOptionButtonInjector.SetLabelViaReflection(go, label);
            var btn = go.GetComponent<CButton>();
            btn.ClearAndAddListener(onClick);
            return btn;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void HideSystemOption()
        {
            if (UIManager.Instance != null && UIElement.SystemOption.Exist)
                UIManager.Instance.HideUI(UIElement.SystemOption);
        }
    }
}
