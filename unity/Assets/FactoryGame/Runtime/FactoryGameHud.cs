using System;
using System.Linq;
using ProjectPenguin.Domain.Factory;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectPenguin.FactoryGame
{
    internal sealed class FactoryGameHud
    {
        private readonly FactoryGameDriver _driver;
        private readonly Font _font;
        private readonly Text _resources, _progress, _message, _selection, _build, _stock, _birds, _telemetry, _stepStatus, _zoom, _pause, _transport;
        private readonly InputField _quantity, _barcode, _minutes;
        private readonly Toggle _recordConfirmed;
        private readonly RectTransform _safe;
        private readonly string[] _recipeIds = { "fishery", "kelp_farm", "saltworks", "preparation", "drying", "kamaboko", "chikuwa", "warehouse" };
        private readonly string[] _resourceIds;
        private int _recipeIndex, _resourceIndex;
        private readonly Text _deliveryItem;
        private readonly Text _recipeInfo, _formationInfo;
        private readonly GameObject _confirmation;
        private Action _pending;
        private static readonly Color Ink = new Color(.08f, .19f, .25f);
        private static readonly Color Accent = new Color(.12f, .46f, .43f);

        internal FactoryGameHud(Transform parent, Font font, FactoryGameDriver driver)
        {
            _driver = driver; _font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resourceIds = new[] { "fish", "kelp", "salt", "surimi", "dried", "kamaboko", "chikuwa", "gift", "research", "oden" };
            var canvas = new GameObject("FactoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            _safe = Panel("SafeArea", canvas.transform, new Color(0, 0, 0, 0));
            UpdateSafeArea();
            var top = Panel("Header", _safe, new Color(.94f, .98f, 1)); Anchors(top, 0, .82f, 1, 1);
            var topStack = Stack(top, 20);
            Label(topStack, "FACTORY ISLAND · 数値は試作設定", 35, 60);
            _resources = Label(topStack, "", 30, 48);
            _progress = Label(topStack, "", 24, 42);
            var tools = Row(topStack);
            _zoom = Button(tools, "組み合わせ表示", driver.ToggleOverview);
            Button(tools, "全体を表示", driver.FitCamera);
            _pause = Button(tools, "一時停止", driver.TogglePause);
            var notice = Panel("Message", _safe, new Color(.87f, .95f, .94f)); Anchors(notice, 0, .75f, 1, .82f);
            _message = Label(notice, "工場を読み込み中", 25, 0); Stretch(_message.rectTransform, 20);

            var lower = Panel("Controls", _safe, new Color(.96f, .98f, 1)); Anchors(lower, 0, 0, 1, .43f);
            var scroll = lower.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Panel("Viewport", lower, Color.white); Stretch(viewport, 0);
            viewport.gameObject.AddComponent<RectMask2D>(); scroll.viewport = viewport;
            var content = Panel("Content", viewport, new Color(0, 0, 0, 0));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>(); Configure(vertical, 16);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            Label(content, "工場と配置 · マスをタップ／ドラッグで移動", 29, 45);
            var lens = Row(content); Button(lens, "寄る", () => driver.ZoomCamera(.8f)); Button(lens, "引く", () => driver.ZoomCamera(1.25f));
            Button(lens, "全体を表示", driver.FitCamera);
            _selection = Label(content, "", 24, 180);
            Button(content, "この区画の上位施設を選択", driver.SelectUpper);
            var choose = Row(content); Button(choose, "施設を切替", () => { _recipeIndex = (_recipeIndex + 1) % _recipeIds.Length; Refresh(); });
            _build = Button(choose, "建設", () => driver.Build(_recipeIds[_recipeIndex]));
            _recipeInfo = Label(content, "", 24, 155);
            _formationInfo = Label(content, "", 24, 240);
            var actions = Row(content); Button(actions, "選択施設をこのマスへ移設", driver.MoveSelected); Button(actions, "稼働／停止", driver.ToggleProduction);
            var rules = driver.Session?.Rules ?? new FactoryRules();
            Label(content, $"エフォート1段階：基本出力+{rules.effortPercent}%・1加工のガス+{rules.effortGas}\n"
                + $"コンボ1棟：出力+{rules.comboPercent}%（上下左右の距離{rules.comboRadius}以内）", 24, 76);
            RectTransform efforts = null;
            for (var i = 0; i <= rules.maxEffort; i++)
            {
                if (i % 4 == 0) efforts = Row(content);
                var effort = i; Button(efforts, "エフォート " + i, () => driver.Effort(effort));
            }
            Label(content, $"2×2の土地拡張 · 水{rules.expandWater} · 選択区画から隣へ", 25, 42);
            var expansion = Row(content);
            Button(expansion, "左", () => driver.Expand(-1, 0)); Button(expansion, "上", () => driver.Expand(0, 1));
            Button(expansion, "下", () => driver.Expand(0, -1)); Button(expansion, "右", () => driver.Expand(1, 0));

            Label(content, "レベル上げの納品", 29, 45);
            _stock = Label(content, "", 24, 330);
            _transport = Label(content, "", 23, 180);
            var deliver = Row(content);
            _deliveryItem = Button(deliver, "魚", () => { _resourceIndex = (_resourceIndex + 1) % _resourceIds.Length; Refresh(); });
            _quantity = Input(deliver, "数量", InputField.ContentType.IntegerNumber); _quantity.text = "100";
            Button(deliver, "納品を確認", () =>
            {
                if (!long.TryParse(_quantity.text, out var amount) || amount <= 0) { driver.Notify("正の納品数量を入力してください"); return; }
                var resource = _resourceIds[_resourceIndex];
                Confirm($"{driver.Session.Catalog.Resource(resource).label}を{amount:N0}個消費して納品します", () => driver.Deliver(resource, amount));
            });

            Label(content, "日常の入力", 29, 45);
            _stepStatus = Label(content, "", 24, 76); Button(content, "実歩数を取得する（iOS）", driver.RequestSteps);
            Label(content, "水：商品コードの記録。飲んだ量の測定ではありません", 24, 65);
            Button(content, "バーコードをカメラで読み取る（iOS）", driver.ScanBarcode);
            var waterRow = Row(content); _barcode = Input(waterRow, "商品バーコード", InputField.ContentType.IntegerNumber);
            _barcode.readOnly = !driver.DevelopmentInputs;
            Button(waterRow, "記録を確認", () =>
            {
                if (!FactorySession.ValidBarcode(_barcode.text)) { driver.Notify("チェックディジットを含む商品コードを確認してください"); return; }
                var code = _barcode.text; Confirm("今日の商品記録として受領します。同じ商品は暦日1回の試作値です", () => driver.ConfirmBarcode(code));
            });
            Label(content, "電気：前日のスクリーンタイム画像を端末内で読み取り、対象日と時間を確認", 24, 76);
            Button(content, "スクリーンタイム画像を選択（iOS・端末内）", driver.ReadScreenImage);
            var screenRow = Row(content); _minutes = Input(screenRow, "前日の使用時間（分）", InputField.ContentType.IntegerNumber);
            _minutes.text = "240";
            Button(screenRow, "前日分を確認", () =>
            {
                if (!int.TryParse(_minutes.text, out var minutes) || minutes < 0 || minutes > 1440)
                { driver.Notify("0〜1440分の確定記録を入力してください"); return; }
                Confirm($"前日の使用時間{minutes}分から電気を受領します。画像の対象日と表示時間を確認してください。本人端末の自動検証は未接続です", () => driver.ConfirmScreen(minutes));
            });

            Label(content, "ペンギン図鑑と所持効果", 29, 45);
            Label(content, "同種の率は羽数分を合計。加算枠と倍率枠を掛け合わせ、資源の付与・生産に1回だけ適用", 24, 76);
            _birds = Label(content, "", 24, 730);
            Label(content, "新工場のすれ違い通信は未接続。所持・保証・日次台帳は端末に保存します", 24, 70);
            if (driver.DevelopmentInputs)
            {
                var dev = Row(content);
                Button(dev, "開発用：合成歩数+5000", driver.FakeSteps);
                Button(dev, "開発用：合成すれ違い", driver.FakeEncounter);
                Label(content, "開発用の入力は実測・実通信ではありません", 24, 45);
            }
            _telemetry = Label(content, "", 23, 100);
            Label(content, "保存は操作ごと・生産1秒ごと。中断中の生産と旧セーブ移行は行いません", 24, 65);

            _confirmation = Panel("ConfirmRecord", _safe, new Color(.04f, .12f, .18f, .95f)).gameObject;
            Stretch((RectTransform)_confirmation.transform, 0);
            var dialog = Panel("Dialog", _confirmation.transform, new Color(.96f, .99f, 1)); Anchors(dialog, .05f, .28f, .95f, .72f);
            var dialogStack = Stack(dialog, 28);
            var confirmText = Label(dialogStack, "", 31, 220);
            _confirmationText = confirmText;
            var toggleRoot = Panel("Confirmed", dialogStack, new Color(.87f, .94f, .94f)); Height(toggleRoot.gameObject, 90);
            _recordConfirmed = toggleRoot.gameObject.AddComponent<Toggle>();
            var check = Panel("Check", toggleRoot, Accent); Anchors(check, .02f, .25f, .08f, .75f);
            _recordConfirmed.graphic = check.GetComponent<Image>();
            var toggleLabel = Label(toggleRoot, "内容を確認しました（タップ）", 27, 0); Stretch(toggleLabel.rectTransform, 20);
            var confirmRow = Row(dialogStack); Button(confirmRow, "取消", Cancel);
            Button(confirmRow, "確定して保存", () =>
            {
                if (!_recordConfirmed.isOn) return;
                var action = _pending; Cancel(); action?.Invoke();
            });
            _confirmation.SetActive(false);
        }

        private readonly Text _confirmationText;
        internal void SetBarcode(string value) { _barcode.text = value; _driver.Notify("商品コードを読み取りました。内容を確認して受領してください"); }
        internal void SetScreenMinutes(int minutes) { _minutes.text = minutes.ToString(); _driver.Notify("画像から使用時間の候補を読み取りました。前日の記録と時間を確認し、必要なら訂正してください"); }
        private void Confirm(string text, Action action)
        { _pending = action; _confirmationText.text = text; _recordConfirmed.isOn = false; _confirmation.SetActive(true); }
        private void Cancel() { _pending = null; _confirmation.SetActive(false); }
        private void Refresh() { if (_driver.Session != null) Render(_driver.Session.Snapshot); }

        internal void ShowFailure(string message)
        {
            _message.text = message;
            foreach (var selectable in _safe.GetComponentsInChildren<Selectable>(true)) selectable.interactable = false;
        }
        internal void Render(FactoryState s)
        {
            UpdateSafeArea(); var catalog = _driver.Session.Catalog;
            _resources.text = $"ガス {s.gas:N0}　電気 {s.electric:N0}　水 {s.water:N0}";
            var next = s.stage < 4 ? _driver.Session.Rules.stagePoints[s.stage].ToString("N0") : "最終試作段階";
            _progress.text = $"段階 S{s.stage} · 累積納品 {s.points:N0}点 · 次 {next} · 土地{s.blocks.Count}区画";
            _message.text = _driver.Message;
            _zoom.text = _driver.Overview ? "内部を編集" : "組み合わせ表示";
            _pause.text = _driver.Paused ? "生産を再開" : "一時停止";
            var selected = s.facilities.FirstOrDefault(n => n.id == _driver.SelectedId);
            _selection.text = $"配置先 ({_driver.CellX},{_driver.CellY})　選択：" + (selected == null ? "なし" :
                $"{(selected.recipe == "warehouse" ? "倉庫" : catalog.Recipe(selected.recipe).label)} Lv{selected.level}\n"
                + $"エフォート{selected.effort} · コンボ{_driver.Session.ComboCount(s, selected)}棟 · {_driver.Session.StopReason(s, selected)}\n"
                + "在庫：" + string.Join(" / ", selected.stock.Where(a => a.amount > 0).Select(a => $"{catalog.Resource(a.id).label}{a.amount:N0}"))
                + $"\n入庫予約：{s.cargo.Where(c => c.to == selected.id).Sum(c => c.amount):N0}個 · 加工残り{Math.Max(0, selected.processEnd - s.atMs) / 1000}秒");
            var recipe = _recipeIds[_recipeIndex];
            _build.text = $"{(recipe == "warehouse" ? "倉庫" : catalog.Recipe(recipe).label)}を建設 · 電気{_driver.Session.NextBuildCost}";
            var r = catalog.Recipe(recipe);
            _recipeInfo.text = r == null ? $"倉庫：資源を保管し自動配送。基本中継{catalog.warehouseRelay * catalog.Growth(s.stage):N0}個/秒" :
                $"{(r.tier <= s.stage ? "建設可能" : $"S{r.tier}で解放")} · 基礎Lv{s.stage} · 1加工{r.durationMs / 1000}秒\n"
                + "材料：" + (r.inputs.Length == 0 ? "なし" : string.Join(" / ", r.inputs.Select(a => $"{catalog.Resource(a.id).label}{a.amount:N0}")))
                + $"\n基本出力：{catalog.Resource(r.output).label}{r.yieldAmount * catalog.Growth(s.stage):N0}個 · 消費：ガス{r.energy[0]} 電気{r.energy[1]} 水{r.energy[2]}\n"
                + "出力にエフォート・コンボ・所持効果を適用。エフォートは追加ガスを消費";
            var blockX = (int)Math.Floor(_driver.CellX / 2.0); var blockY = (int)Math.Floor(_driver.CellY / 2.0);
            var present = s.facilities.Where(n => !n.upper && (int)Math.Floor(n.x / 2.0) == blockX && (int)Math.Floor(n.y / 2.0) == blockY)
                .Select(n => n.recipe).ToArray();
            _formationInfo.text = $"2×2区画 ({blockX},{blockY}) · 順序・向き不問\n" + string.Join("\n", FactoryCatalog.Formations.Select(f =>
            {
                var missing = f.Missing(present); var stage = catalog.Recipe(f.Recipe)?.tier ?? 1;
                var label = f.Recipe == "warehouse" ? "大型倉庫" : catalog.Recipe(f.Recipe).label;
                return label + "：" + (missing.Length == 0 ? (s.stage >= stage ? "成立" : $"構成一致・S{stage}待ち") :
                    "不足 " + string.Join("・", missing.Select(id => id == "warehouse" ? "倉庫" : catalog.Recipe(id).label)) + $" / S{stage}");
            }));
            _stock.text = string.Join("\n", catalog.resources.Select(r =>
                $"{r.label}：在庫{s.facilities.Sum(f => FactoryState.Amount(f.stock, r.id)):N0} / 配送{s.cargo.Where(c => c.resource == r.id).Sum(c => c.amount):N0}"));
            _transport.text = s.cargo.Count == 0 ? "配送中の荷物なし。隣接する施設から必要な材料を自動配送します" :
                "配送中（先頭5便）\n" + string.Join("\n", s.cargo.Take(5).Select(c =>
                    $"施設{c.from}→{c.to}：{catalog.Resource(c.resource).label}{c.amount:N0}個 · あと{(c.arrivesAt - s.atMs) / 1000}秒"));
            _deliveryItem.text = catalog.Resource(_resourceIds[_resourceIndex]).label;
            _stepStatus.text = _driver.StepStatus;
            _birds.text = $"今日の獲得{s.dailyBirds}/{catalog.dailyBirds}羽 · 新種なし{s.pityMisses}/{catalog.pityAfter}回\n" + string.Join("\n", catalog.penguins.Select(p =>
                $"{p.label}：{(p.stage <= s.stage ? $"{FactoryState.Amount(s.penguins, p.id)}羽 / 通常{_driver.Session.SpeciesProbability(p.id):P1}" : $"S{p.stage}で種類解放")} · "
                + $"{(catalog.Resource(p.target)?.label ?? (p.target == "gas" ? "ガス" : p.target == "electric" ? "電気" : "水"))}{(p.multiplicative ? "倍率枠" : "加算枠")}+{p.percent}%/羽"));
            _telemetry.text = $"施設{s.facilities.Count}棟 · 配送{s.cargo.Count}便 · 計算 {_driver.SimulationMilliseconds:F2}ms / 表示更新 {_driver.RenderMilliseconds:F2}ms\n"
                + "表示更新時間はGPU・フレーム時間の実測とは別です";
        }

        private void UpdateSafeArea()
        {
            var area = Screen.safeArea;
            _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }
        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = color.a > 0;
            return (RectTransform)go.transform;
        }
        private static void Anchors(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Stretch(RectTransform rect, int padding)
        { Anchors(rect, 0, 0, 1, 1); rect.offsetMin = new Vector2(padding, padding); rect.offsetMax = new Vector2(-padding, -padding); }
        private static void Height(GameObject go, int height)
        { var layout = go.AddComponent<LayoutElement>(); layout.minHeight = height; layout.preferredHeight = height; layout.flexibleWidth = 1; }
        private static void Configure(HorizontalOrVerticalLayoutGroup group, int spacing)
        { group.spacing = spacing; group.padding = new RectOffset(16, 16, 10, 10); group.childControlWidth = group.childControlHeight = true; group.childForceExpandHeight = false; }
        private static RectTransform Stack(Transform parent, int spacing)
        {
            var stack = Panel("Stack", parent, new Color(0, 0, 0, 0)); Stretch(stack, 0);
            Configure(stack.gameObject.AddComponent<VerticalLayoutGroup>(), spacing); return stack;
        }
        private static RectTransform Row(Transform parent)
        { var row = Panel("Row", parent, new Color(0, 0, 0, 0)); Height(row.gameObject, 86); Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 10); return row; }
        private Text Label(Transform parent, string text, int size, int height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>(); label.font = _font; label.text = text; label.fontSize = size; label.color = Ink;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (height > 0) Height(go, height); return label;
        }
        private Text Button(Transform parent, string text, UnityAction action)
        {
            var panel = Panel("Button", parent, Accent); Height(panel.gameObject, 72);
            var button = panel.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
            var label = Label(panel, text, 25, 0); label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = 25; Stretch(label.rectTransform, 8); return label;
        }
        private InputField Input(Transform parent, string placeholder, InputField.ContentType type)
        {
            var panel = Panel("Input", parent, new Color(.86f, .92f, .94f)); Height(panel.gameObject, 72);
            var field = panel.gameObject.AddComponent<InputField>(); field.contentType = type;
            var text = Label(panel, "", 28, 0); Stretch(text.rectTransform, 10); field.textComponent = text;
            var hint = Label(panel, placeholder, 24, 0); hint.color = new Color(.3f, .4f, .45f); Stretch(hint.rectTransform, 10); field.placeholder = hint;
            return field;
        }
    }
}
