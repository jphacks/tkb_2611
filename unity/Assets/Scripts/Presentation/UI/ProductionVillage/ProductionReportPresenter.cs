using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.Industrial;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 全画面生産レポート画面の Presenter。
    /// ガスのタンク・生産と消費の推移・品目ごとの実績と理論値を表示する。
    /// </summary>
    public sealed class ProductionReportPresenter
    {
        private enum SortMode { Standard, DeficitFirst, HighProduction }
        private enum FilterMode { All, Active, Stopped }
        private enum SpanMode { Min10, Min60 }

        private readonly VisualElement _root;
        private readonly ProductionVillageSession _session;

        // 見出し・タブ・閉じる
        private readonly VisualElement _powerSection;
        private readonly Button _tabPower;
        private readonly Button _tabItems;
        private readonly Button _closeBtn;

        // タンク部
        private readonly TankGauge _tankGauge;
        private readonly Label _supplyNum;
        private readonly Label _consumeNum;
        private readonly Label _gasStock;
        private readonly Label _gasCap;
        private readonly Label _netNum;
        private readonly Label _emptyEstimate;

        // 状態チップ
        private readonly Label _chipActiveNum;
        private readonly Label _chipActiveTotal;
        private readonly Label _chipStoppedNum;
        private readonly Label _chipStoppedReason;
        private readonly VisualElement _chipStoppedContainer;
        private readonly Label _chipCargoNum;

        // 表部
        private readonly Label _sectionTitle;
        private readonly ScrollView _scroll;
        private readonly Label _emptyNotice;

        // 下部バー
        private readonly Button _sortBtn;
        private readonly Button _filterBtn;
        private readonly Button _spanBtn;

        // 状態
        private SortMode _sort = SortMode.Standard;
        private FilterMode _filter = FilterMode.All;
        private SpanMode _span = SpanMode.Min10;
        private bool _isPowerTab = true;

        // 行キャッシュ (品目ごと)
        private readonly Dictionary<Item, RowViewHolder> _rowHolders = new Dictionary<Item, RowViewHolder>();

        public bool IsOpen => _root != null && !_root.ClassListContains(UiClasses.Hidden);

        public ProductionReportPresenter(VisualElement root, ProductionVillageSession session)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            _powerSection = _root.Q(ProductionReportNames.PowerSection);
            _tabPower = _root.Q<Button>(ProductionReportNames.TabPower);
            _tabItems = _root.Q<Button>(ProductionReportNames.TabItems);
            _closeBtn = _root.Q<Button>(ProductionReportNames.Close);

            _tankGauge = _root.Q<TankGauge>(ProductionReportNames.TankGauge);
            _supplyNum = _root.Q<Label>(ProductionReportNames.SupplyNum);
            _consumeNum = _root.Q<Label>(ProductionReportNames.ConsumeNum);
            _gasStock = _root.Q<Label>(ProductionReportNames.GasStock);
            _gasCap = _root.Q<Label>(ProductionReportNames.GasCap);
            _netNum = _root.Q<Label>(ProductionReportNames.NetPillNum);
            _emptyEstimate = _root.Q<Label>(ProductionReportNames.EmptyEstimate);

            _chipActiveNum = _root.Q<Label>(ProductionReportNames.ChipActiveNum);
            _chipActiveTotal = _root.Q<Label>(ProductionReportNames.ChipActiveTotal);
            _chipStoppedNum = _root.Q<Label>(ProductionReportNames.ChipStoppedNum);
            _chipStoppedReason = _root.Q<Label>(ProductionReportNames.ChipStoppedReason);
            _chipStoppedContainer = _chipStoppedNum?.parent;
            _chipCargoNum = _root.Q<Label>(ProductionReportNames.ChipCargoNum);

            _sectionTitle = _root.Q<Label>(ProductionReportNames.SectionTitle);
            _scroll = _root.Q<ScrollView>(ProductionReportNames.Scroll);
            _emptyNotice = _root.Q<Label>(ProductionReportNames.EmptyNotice);

            _sortBtn = _root.Q<Button>(ProductionReportNames.SortBtn);
            _filterBtn = _root.Q<Button>(ProductionReportNames.FilterBtn);
            _spanBtn = _root.Q<Button>(ProductionReportNames.SpanBtn);

            BindControls();
            BuildRowHolders();
        }

        private void BindControls()
        {
            if (_closeBtn != null) _closeBtn.clicked += Close;
            if (_tabPower != null) _tabPower.clicked += () => SetPowerTab(true);
            if (_tabItems != null) _tabItems.clicked += () => SetPowerTab(false);

            if (_sortBtn != null)
            {
                _sortBtn.clicked += () =>
                {
                    _sort = (SortMode)(((int)_sort + 1) % 3);
                    UpdateSortButton();
                    Refresh();
                };
            }

            if (_filterBtn != null)
            {
                _filterBtn.clicked += () =>
                {
                    _filter = (FilterMode)(((int)_filter + 1) % 3);
                    UpdateFilterButton();
                    Refresh();
                };
            }

            if (_spanBtn != null)
            {
                _spanBtn.clicked += () =>
                {
                    _span = _span == SpanMode.Min10 ? SpanMode.Min60 : SpanMode.Min10;
                    UpdateSpanButton();
                    Refresh();
                };
            }

            UpdateSortButton();
            UpdateFilterButton();
            UpdateSpanButton();
        }

        public void Open()
        {
            if (_root == null) return;
            IslandUi.SetVisible(_root, true);
            _tankGauge?.StartAnimation();
            Refresh();
            AnimateRowsEntrance();
        }

        public void Close()
        {
            if (_root == null) return;
            IslandUi.SetVisible(_root, false);
            _tankGauge?.StopAnimation();
        }

        private void SetPowerTab(bool isPower)
        {
            _isPowerTab = isPower;
            _tabPower?.EnableInClassList(ProductionReportNames.SegItemActiveClass, isPower);
            _tabItems?.EnableInClassList(ProductionReportNames.SegItemActiveClass, !isPower);
            IslandUi.SetVisible(_powerSection, isPower);
        }

        private void UpdateSortButton()
        {
            if (_sortBtn == null) return;
            _sortBtn.text = _sort switch
            {
                SortMode.Standard => "並び順: 標準 ▼",
                SortMode.DeficitFirst => "並び順: 不足が先 ▼",
                SortMode.HighProduction => "並び順: 生産順 ▼",
                _ => "並び順 ▼"
            };
        }

        private void UpdateFilterButton()
        {
            if (_filterBtn == null) return;
            _filterBtn.text = _filter switch
            {
                FilterMode.All => "絞り込み: すべて ▼",
                FilterMode.Active => "絞り込み: 稼働中 ▼",
                FilterMode.Stopped => "絞り込み: 停止中 ▼",
                _ => "絞り込み ▼"
            };
        }

        private void UpdateSpanButton()
        {
            if (_spanBtn == null) return;
            _spanBtn.text = _span == SpanMode.Min10 ? "期間: 10 分 ▼" : "期間: 60 分 ▼";
            if (_sectionTitle != null)
            {
                _sectionTitle.text = _span == SpanMode.Min10 ? "品目ごと (直近 10 分)" : "品目ごと (直近 60 分)";
            }
        }

        public void Refresh()
        {
            if (!IsOpen) return;
            var core = _session.Core;
            var view = core?.View;
            if (view == null) return;

            UpdateTankAndGas(view, core);
            UpdateStatusChips(view);
            UpdateTable(view);
        }

        private void UpdateTankAndGas(VillageView view, ProductionVillageSessionCore core)
        {
            var isUnlimited = view.Unlimited;
            var minutesSinceMidnight = Math.Max(60.0, (DateTime.Now - DateTime.Today).TotalMinutes);
            var supply = VillageInsight.SupplyPerMinute(view.GasReceivedToday, minutesSinceMidnight);
            var consume = view.GasPerMinute;
            var net = supply - consume;

            if (_supplyNum != null) _supplyNum.text = supply > 0 ? supply.ToString("0.#") : "0";
            if (_consumeNum != null) _consumeNum.text = consume > 0 ? consume.ToString("0.#") : "0";

            if (isUnlimited)
            {
                if (_gasStock != null) _gasStock.text = "∞";
                if (_gasCap != null) _gasCap.text = "";
                if (_netNum != null) _netNum.text = "無制限";
                if (_emptyEstimate != null) _emptyEstimate.text = "開発用: 無制限";
                _tankGauge?.SetLevel(1f, true, consume > 0.01);
                return;
            }

            if (_gasStock != null) _gasStock.text = IslandUi.Number(view.Gas);

            var capacity = VillageInsight.TankCapacity(consume);
            if (_gasCap != null)
            {
                _gasCap.text = capacity.HasValue ? $"/ {IslandUi.Number(capacity.Value)}" : "";
            }

            var level = VillageInsight.TankLevel(view.Gas, consume);
            _tankGauge?.SetLevel((float)level, supply > 0.01, consume > 0.01);

            // 収支札
            if (_netNum != null)
            {
                var sign = net > 0.05 ? "+" : (net < -0.05 ? "-" : "±");
                _netNum.text = $"{sign}{Math.Abs(net):0.#}";
            }

            if (_emptyEstimate != null)
            {
                var emptyMin = VillageInsight.MinutesUntilEmpty(view.Gas, net);
                if (emptyMin.HasValue)
                {
                    _emptyEstimate.text = $"あと {emptyMin.Value} 分で空になる";
                }
                else
                {
                    _emptyEstimate.text = "いまは減っていない";
                }
            }
        }

        private void UpdateStatusChips(VillageView view)
        {
            var totalFacilities = view.Nodes.Count;
            var activeCount = view.Nodes.Count(n => n.Status == "生産中");
            var stoppedCount = view.Nodes.Count(n => n.Status != "生産中" && n.Kind < FacilityKind.Warehouse);

            if (_chipActiveNum != null) _chipActiveNum.text = $"{activeCount}";
            if (_chipActiveTotal != null) _chipActiveTotal.text = $"/{Math.Max(1, totalFacilities)}";

            if (_chipStoppedNum != null) _chipStoppedNum.text = $"{stoppedCount}";
            if (_chipStoppedReason != null)
            {
                if (stoppedCount == 0)
                {
                    _chipStoppedReason.text = "順調";
                    _chipStoppedContainer?.RemoveFromClassList(ProductionReportNames.ChipBadClass);
                }
                else
                {
                    // 停止原因の優先度判定
                    var reasons = view.Nodes.Where(n => n.Status != "生産中" && n.Kind < FacilityKind.Warehouse)
                        .Select(n => n.Status)
                        .ToList();
                    if (reasons.Any(s => s.Contains("ガス") || s.Contains("エネルギー")))
                    {
                        _chipStoppedReason.text = "ガス不足";
                    }
                    else if (reasons.Any(s => s.Contains("材料") || s.Contains("待機")))
                    {
                        _chipStoppedReason.text = "材料待ち";
                    }
                    else if (reasons.Any(s => s.Contains("満杯") || s.Contains("いっぱい")))
                    {
                        _chipStoppedReason.text = "満杯";
                    }
                    else
                    {
                        _chipStoppedReason.text = reasons.FirstOrDefault() ?? "停止";
                    }
                    _chipStoppedContainer?.AddToClassList(ProductionReportNames.ChipBadClass);
                }
            }

            if (_chipCargoNum != null) _chipCargoNum.text = $"{view.Cargo.Count}";
        }

        private void BuildRowHolders()
        {
            if (_scroll == null) return;
            foreach (var item in (Item[])Enum.GetValues(typeof(Item)))
            {
                var holder = CreateRowHolder(item);
                _rowHolders[item] = holder;
                _scroll.Add(holder.Root);
            }
        }

        private RowViewHolder CreateRowHolder(Item item)
        {
            var row = new VisualElement();
            row.AddToClassList(ProductionReportNames.RowClass);

            // Item Meta
            var itemCol = new VisualElement();
            itemCol.AddToClassList("pp-ind-item");

            var tile = new VisualElement();
            tile.AddToClassList("pp-ind-tile");
            tile.style.borderBottomColor = ColorOfItem(item);

            var icon = new VisualElement();
            icon.AddToClassList("pp-report-icon");
            icon.AddToClassList(UiClasses.Thumb.Root);
            icon.AddToClassList(UiClasses.Thumb.OfItem(item));
            icon.AddToClassList(IslandUi.IconClass(item));
            icon.style.unityBackgroundImageTintColor = ColorOfItem(item);
            tile.Add(icon);
            itemCol.Add(tile);

            var meta = new VisualElement();
            meta.AddToClassList("pp-ind-meta");

            var nameLabel = new Label(IslandCatalog.NameOf(item));
            nameLabel.AddToClassList("pp-ind-name");
            meta.Add(nameLabel);

            var stockRow = new VisualElement();
            stockRow.AddToClassList("pp-ind-stock");
            var stockLabel = new Label("在庫");
            stockLabel.AddToClassList("pp-ind-stock__label");
            var stockVal = new Label("0");
            stockVal.AddToClassList("pp-ind-stock__val");
            var stockCap = new Label("/ 0");
            stockCap.AddToClassList("pp-ind-stock__cap");
            stockRow.Add(stockLabel);
            stockRow.Add(stockVal);
            stockRow.Add(stockCap);
            meta.Add(stockRow);

            itemCol.Add(meta);
            row.Add(itemCol);

            // Trend Bars
            var trendCol = new VisualElement();
            trendCol.AddToClassList("pp-ind-trend");
            var flowBars = new FlowBars();
            flowBars.AddToClassList("pp-ind-flow-bars");
            trendCol.Add(flowBars);
            row.Add(trendCol);

            // Rates / Stop
            var ratesCol = new VisualElement();
            ratesCol.AddToClassList("pp-ind-rates");

            // Normal Rates Container
            var normalRates = new VisualElement();

            // Prod Meter
            var prodMeter = new VisualElement();
            prodMeter.AddToClassList("pp-ind-meter");
            prodMeter.AddToClassList("pp-ind-meter--p");
            var prodLabel = new Label("生産");
            prodLabel.AddToClassList("pp-ind-meter__label");
            var prodNow = new Label("0");
            prodNow.AddToClassList("pp-ind-meter__now");
            var prodTheo = new Label("/ 0");
            prodTheo.AddToClassList("pp-ind-meter__theo");
            var prodValues = new VisualElement();
            prodValues.AddToClassList("pp-ind-meter__values");
            prodValues.Add(prodLabel);
            prodValues.Add(prodNow);
            prodValues.Add(prodTheo);
            prodMeter.Add(prodValues);

            var prodTrack = new VisualElement();
            prodTrack.AddToClassList("pp-report-bar-track");
            var prodFill = new VisualElement();
            prodFill.AddToClassList("pp-report-bar-fill");
            prodFill.AddToClassList("pp-report-bar-fill--prod");
            prodTrack.Add(prodFill);
            prodMeter.Add(prodTrack);
            normalRates.Add(prodMeter);

            // Cons Meter
            var consMeter = new VisualElement();
            consMeter.AddToClassList("pp-ind-meter");
            consMeter.AddToClassList("pp-ind-meter--c");
            var consLabel = new Label("消費");
            consLabel.AddToClassList("pp-ind-meter__label");
            var consNow = new Label("0");
            consNow.AddToClassList("pp-ind-meter__now");
            var consTheo = new Label("/ 0");
            consTheo.AddToClassList("pp-ind-meter__theo");
            var consValues = new VisualElement();
            consValues.AddToClassList("pp-ind-meter__values");
            consValues.Add(consLabel);
            consValues.Add(consNow);
            consValues.Add(consTheo);
            consMeter.Add(consValues);

            var consTrack = new VisualElement();
            consTrack.AddToClassList("pp-report-bar-track");
            var consFill = new VisualElement();
            consFill.AddToClassList("pp-report-bar-fill");
            consFill.AddToClassList("pp-report-bar-fill--cons");
            consTrack.Add(consFill);
            consMeter.Add(consTrack);
            normalRates.Add(consMeter);

            // Deficit tag
            var shortTag = new Label("不足");
            shortTag.AddToClassList("pp-ind-short-tag");
            normalRates.Add(shortTag);

            ratesCol.Add(normalRates);

            // Stop Container
            var stopContainer = new VisualElement();
            stopContainer.AddToClassList("pp-ind-stop");
            var stopTag = new Label("停止中");
            stopTag.AddToClassList("pp-ind-stop-tag");
            var stopReason = new Label("ガスが足りない");
            stopReason.AddToClassList("pp-ind-stop__reason");
            var stopHint = new Label("歩くとガスがたまるよ");
            stopHint.AddToClassList("pp-ind-stop__hint");
            stopContainer.Add(stopTag);
            stopContainer.Add(stopReason);
            stopContainer.Add(stopHint);

            ratesCol.Add(stopContainer);
            row.Add(ratesCol);

            return new RowViewHolder
            {
                Item = item,
                Root = row,
                StockVal = stockVal,
                StockCap = stockCap,
                FlowBars = flowBars,
                NormalRates = normalRates,
                ProdNow = prodNow,
                ProdTheo = prodTheo,
                ProdFill = prodFill,
                ConsNow = consNow,
                ConsTheo = consTheo,
                ConsFill = consFill,
                ShortTag = shortTag,
                StopContainer = stopContainer,
                StopReason = stopReason,
                StopHint = stopHint
            };
        }

        private void UpdateTable(VillageView view)
        {
            var hasAnyFacility = view.Nodes.Count > 0;
            IslandUi.SetVisible(_emptyNotice, !hasAnyFacility);

            var items = (Item[])Enum.GetValues(typeof(Item));
            var rowDataList = new List<RowData>();
            var warehouseCount = view.Nodes.Count(n => n.Kind == FacilityKind.Warehouse);
            var defaultCap = 12 + warehouseCount * 20;
            var trendMap = _span == SpanMode.Min10 ? view.Trend10 : view.Trend60;

            foreach (var item in items)
            {
                view.ItemRates.TryGetValue(item, out var rate);
                var stock = view.Totals.GetValueOrDefault(item);
                var cap = defaultCap;
                int[] made = Array.Empty<int>();
                int[] used = Array.Empty<int>();
                if (trendMap != null && trendMap.TryGetValue(item, out var trend))
                {
                    made = trend.Made ?? Array.Empty<int>();
                    used = trend.Used ?? Array.Empty<int>();
                }

                var theoMake = rate?.TheoreticalMakePerMinute ?? 0;
                var theoUse = rate?.TheoreticalUsePerMinute ?? 0;
                var actualMake = rate?.ActualMakePerMinute ?? 0;
                var actualUse = rate?.ActualUsePerMinute ?? 0;
                var isDeficit = rate?.IsDeficit == true;

                // 停止判定: 理論生産 > 0 かつ 期間内の Made 合計 == 0
                var isStopped = theoMake > 0.01 && made.Sum() == 0;

                // 停止理由 & ヒント
                string stopReasonText = "停止中";
                string stopHintText = "";
                if (isStopped)
                {
                    var producerNode = view.Nodes.FirstOrDefault(n =>
                    {
                        var def = IslandCatalog.Get(n.Kind);
                        return def.Recipes != null && def.Recipes.Any(r => r.Output.Item == item);
                    });
                    if (producerNode != null)
                    {
                        var status = producerNode.Status ?? "";
                        if (status.Contains("ガス") || status.Contains("エネルギー"))
                        {
                            stopReasonText = "ガスが足りない";
                            stopHintText = "歩くとガスがたまるよ";
                        }
                        else if (status.Contains("材料") || status.Contains("待機"))
                        {
                            stopReasonText = "材料を待っている";
                            stopHintText = "前の工程を増やそう";
                        }
                        else if (status.Contains("満杯") || status.Contains("いっぱい"))
                        {
                            stopReasonText = "置き場がいっぱい";
                            stopHintText = "倉庫を建てて保管枠を広げよう";
                        }
                        else
                        {
                            stopReasonText = status;
                            stopHintText = "稼働条件を確認しよう";
                        }
                    }
                }

                rowDataList.Add(new RowData
                {
                    Item = item,
                    Stock = stock,
                    Cap = cap,
                    Made = made,
                    Used = used,
                    TheoMake = theoMake,
                    TheoUse = theoUse,
                    ActualMake = actualMake,
                    ActualUse = actualUse,
                    IsDeficit = isDeficit,
                    IsStopped = isStopped,
                    StopReason = stopReasonText,
                    StopHint = stopHintText
                });
            }

            // フィルタリング
            var filtered = rowDataList.Where(r =>
            {
                return _filter switch
                {
                    FilterMode.All => true,
                    FilterMode.Active => !r.IsStopped && (r.ActualMake > 0 || r.ActualUse > 0 || r.TheoMake > 0),
                    FilterMode.Stopped => r.IsStopped,
                    _ => true
                };
            });

            // ソート
            var sorted = _sort switch
            {
                SortMode.Standard => filtered.OrderBy(r => (int)r.Item),
                SortMode.DeficitFirst => filtered.OrderByDescending(r => r.IsDeficit)
                                                .ThenByDescending(r => r.IsStopped)
                                                .ThenByDescending(r => r.ActualMake),
                SortMode.HighProduction => filtered.OrderByDescending(r => r.ActualMake)
                                                   .ThenByDescending(r => r.TheoMake),
                _ => filtered.OrderBy(r => (int)r.Item)
            };

            // 全行を非表示にしてから、ソート順に再配置 & 表示
            foreach (var kvp in _rowHolders)
            {
                kvp.Value.Root.style.display = DisplayStyle.None;
            }

            foreach (var data in sorted)
            {
                if (!_rowHolders.TryGetValue(data.Item, out var holder)) continue;

                holder.Root.style.display = DisplayStyle.Flex;
                holder.Root.BringToFront();

                // クラス設定
                holder.Root.EnableInClassList(ProductionReportNames.RowDeficitClass, data.IsDeficit);
                holder.Root.EnableInClassList(ProductionReportNames.RowStoppedClass, data.IsStopped);

                // 在庫
                holder.StockVal.text = IslandUi.Number(data.Stock);
                holder.StockCap.text = $"/ {IslandUi.Number(data.Cap)}";

                // フローバー
                holder.FlowBars.SetData(data.Made, data.Used);

                if (data.IsStopped)
                {
                    IslandUi.SetVisible(holder.NormalRates, false);
                    IslandUi.SetVisible(holder.StopContainer, true);
                    holder.StopReason.text = data.StopReason;
                    holder.StopHint.text = data.StopHint;
                }
                else
                {
                    IslandUi.SetVisible(holder.NormalRates, true);
                    IslandUi.SetVisible(holder.StopContainer, false);

                    holder.ProdNow.text = data.ActualMake > 0 ? $"{data.ActualMake:0.#}" : "0";
                    holder.ProdTheo.text = $"/ {data.TheoMake:0.#}";
                    var prodRatio = data.TheoMake > 0 ? Mathf.Clamp01((float)(data.ActualMake / data.TheoMake)) : 0f;
                    holder.ProdFill.style.width = Length.Percent(prodRatio * 100f);

                    holder.ConsNow.text = data.ActualUse > 0 ? $"{data.ActualUse:0.#}" : "0";
                    holder.ConsTheo.text = $"/ {data.TheoUse:0.#}";
                    var consRatio = data.TheoUse > 0 ? Mathf.Clamp01((float)(data.ActualUse / data.TheoUse)) : 0f;
                    holder.ConsFill.style.width = Length.Percent(consRatio * 100f);
                    holder.ConsFill.EnableInClassList("pp-report-bar-fill--bad", data.IsDeficit);

                    IslandUi.SetVisible(holder.ShortTag, data.IsDeficit);
                    if (data.IsDeficit)
                    {
                        var diff = data.TheoUse - data.TheoMake;
                        holder.ShortTag.text = $"不足 −{diff:0.#}/分";
                    }
                }
            }
        }

        private void AnimateRowsEntrance()
        {
            int index = 0;
            foreach (var kvp in _rowHolders)
            {
                var row = kvp.Value.Root;
                if (row.style.display == DisplayStyle.None) continue;
                row.style.opacity = 0f;
                var delay = index * 40;
                row.schedule.Execute(() =>
                {
                    row.style.opacity = 1f;
                }).StartingIn(delay);
                index++;
            }
        }

        private static Color ColorOfItem(Item item) => item switch
        {
            Item.Fish => new Color(0.18f, 0.66f, 0.93f, 1f),       // #2FA8EC
            Item.Surimi => new Color(0.36f, 0.75f, 0.75f, 1f),     // #5BC0BE
            Item.Salt => new Color(0.96f, 0.65f, 0.14f, 1f),       // #F5A623
            Item.Kelp => new Color(0.24f, 0.70f, 0.44f, 1f),       // #3EB370
            Item.Kamaboko => new Color(1.0f, 0.48f, 0.56f, 1f),     // #FF7A90
            Item.Chikuwa => new Color(0.85f, 0.53f, 0.28f, 1f),     // #D98847
            Item.DriedFish => new Color(0.90f, 0.49f, 0.13f, 1f),   // #E67E22
            Item.Oden => new Color(0.92f, 0.35f, 0.25f, 1f),       // #EA5840
            _ => new Color(0.18f, 0.66f, 0.93f, 1f)
        };

        private sealed class RowViewHolder
        {
            public Item Item;
            public VisualElement Root;
            public Label StockVal;
            public Label StockCap;
            public FlowBars FlowBars;
            public VisualElement NormalRates;
            public Label ProdNow;
            public Label ProdTheo;
            public VisualElement ProdFill;
            public Label ConsNow;
            public Label ConsTheo;
            public VisualElement ConsFill;
            public Label ShortTag;
            public VisualElement StopContainer;
            public Label StopReason;
            public Label StopHint;
        }

        private sealed class RowData
        {
            public Item Item;
            public long Stock;
            public long Cap;
            public int[] Made;
            public int[] Used;
            public double TheoMake;
            public double TheoUse;
            public double ActualMake;
            public double ActualUse;
            public bool IsDeficit;
            public bool IsStopped;
            public string StopReason;
            public string StopHint;
        }
    }
}
