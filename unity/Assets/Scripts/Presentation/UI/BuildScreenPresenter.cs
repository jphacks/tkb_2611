using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 建てる流れの 2 画面を受け持つ。建物の一覧 (棚) → 1 つ押すと説明の画面 → 丸いボタンで島へ戻って置き場所を選ぶ。
    /// 説明の画面は左右のカード (またはスワイプ) で隣の建物へ移れる。最後の 1 枚は「島を広げる」。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BuildScreenPresenter : MonoBehaviour
    {
        /// <summary>これより長く横に動かしたら、隣の建物へ移る (px)。</summary>
        private const float SwipeThreshold = 80f;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandInteraction _interaction;
        [SerializeField] private IslandSheetPresenter _sheets;
        [SerializeField] private IslandHudPresenter _hud;

        private readonly List<FacilityDef> _defs = new List<FacilityDef>();
        private VisualElement _shelf;
        private ScrollView _scroll;

        private VisualElement _stage;
        private VisualElement _mainCard;
        private VisualElement _mainImage;
        private VisualElement _mainLock;
        private Label _mainLockLabel;
        private VisualElement _prevCard;
        private VisualElement _prevImage;
        private VisualElement _nextCard;
        private VisualElement _nextImage;
        private Label _name;
        private VisualElement _time;
        private Label _timeValue;
        private Label _lead;
        private VisualElement _recipe;
        private VisualElement _costs;
        private Label _owned;
        private Button _buildButton;
        private Label _buildLabel;

        private int _index;
        private Vector2 _pointerStart;
        private bool _pointerDown;

        /// <summary>棚の最後の「島を広げる」の番号。</summary>
        private int ExpandIndex => _defs.Count;

        private void OnEnable()
        {
            if (_game == null || _navigator == null || _interaction == null)
            {
                Debug.LogError($"{nameof(BuildScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _defs.Clear();
            // 開く順 (お魚の桁) に並べる。同じ段の中は表の順 (最初は氷室)。
            _defs.AddRange(IslandCatalog.Facilities.OrderBy(f => f.UnlockFish));

            var root = GetComponent<UIDocument>().rootVisualElement;
            var list = root.Q(ScreenNavigator.ElementNameOf(AppScreen.Build));
            _shelf = list.Q(UiNames.Build.BuildShelf);
            _scroll = list.Q<ScrollView>(UiNames.Build.BuildScroll);

            var detail = root.Q(ScreenNavigator.ElementNameOf(AppScreen.BuildDetail));
            _stage = detail.Q(UiNames.BuildDetail.DetailStage);
            _mainCard = detail.Q(UiNames.BuildDetail.MainCard);
            _mainImage = detail.Q(UiNames.BuildDetail.MainImage);
            _mainLock = detail.Q(UiNames.BuildDetail.MainLock);
            _mainLockLabel = detail.Q<Label>(UiNames.BuildDetail.MainLockLabel);
            _prevCard = detail.Q(UiNames.BuildDetail.PrevCard);
            _prevImage = detail.Q(UiNames.BuildDetail.PrevImage);
            _nextCard = detail.Q(UiNames.BuildDetail.NextCard);
            _nextImage = detail.Q(UiNames.BuildDetail.NextImage);
            _name = detail.Q<Label>(UiNames.BuildDetail.DetailName);
            _time = detail.Q(UiNames.BuildDetail.DetailTime);
            _timeValue = detail.Q<Label>(UiNames.BuildDetail.DetailTimeValue);
            _lead = detail.Q<Label>(UiNames.BuildDetail.DetailLead);
            _recipe = detail.Q(UiNames.BuildDetail.DetailRecipe);
            _costs = detail.Q(UiNames.BuildDetail.DetailCosts);
            _owned = detail.Q<Label>(UiNames.BuildDetail.DetailOwned);
            _buildButton = detail.Q<Button>(UiNames.BuildDetail.BuildButton);
            _buildLabel = detail.Q<Label>(UiNames.BuildDetail.BuildButtonLabel);

            _buildButton.clicked += OnBuild;
            _prevCard.RegisterCallback<ClickEvent>(OnPrev);
            _nextCard.RegisterCallback<ClickEvent>(OnNext);
            _stage.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _stage.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _stage.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _navigator.ScreenShown += OnScreenShown;
            _game.IslandChanged += OnIslandChanged;
        }

        private void OnDisable()
        {
            if (_buildButton != null)
            {
                _buildButton.clicked -= OnBuild;
                _prevCard.UnregisterCallback<ClickEvent>(OnPrev);
                _nextCard.UnregisterCallback<ClickEvent>(OnNext);
                _stage.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _stage.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _stage.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }

            if (_game != null)
            {
                _game.IslandChanged -= OnIslandChanged;
            }
        }

        // ------------------------------------------------------------------
        // 公開
        // ------------------------------------------------------------------

        /// <summary>建物の一覧を開く。</summary>
        public void Open() => _navigator.Show(AppScreen.Build);

        /// <summary>建物の説明を直接開く (最初の漁場への案内など)。</summary>
        public void OpenDetail(FacilityKind kind)
        {
            var i = _defs.FindIndex(d => d.Kind == kind);
            ShowDetail(i < 0 ? 0 : i);
        }

        // ------------------------------------------------------------------
        // 一覧
        // ------------------------------------------------------------------

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Build)
            {
                RebuildShelf();
                if (previous != AppScreen.BuildDetail)
                {
                    _scroll.scrollOffset = Vector2.zero;
                }
            }
            else if (screen == AppScreen.BuildDetail)
            {
                RefreshDetail();
            }
        }

        private void OnIslandChanged()
        {
            // 説明の画面は数字 (足りる・足りない) が変わるので開いている間は更新する。棚は押している途中に作り直さない。
            if (_navigator.Current == AppScreen.BuildDetail)
            {
                RefreshDetail(false);
            }
        }

        private void RebuildShelf()
        {
            _shelf.Clear();
            var island = _game.Island;
            for (var i = 0; i < _defs.Count; i++)
            {
                var def = _defs[i];
                var unlocked = island.IsUnlocked(def.Kind);
                var count = island.CountOf(def.Kind);
                var card = Card(UiClasses.Thumb.Of(def.Kind), def.Name, i);
                card.EnableInClassList(UiClasses.ShelfCard.Locked, !unlocked);
                var thumb = card.Q(className: UiClasses.ShelfCard.Thumb);
                if (!unlocked)
                {
                    thumb.Add(LockMark(IslandUi.UnlockShort(def.UnlockFish), true));
                }
                else if (count > 0)
                {
                    card.Add(IslandUi.Text(count.ToString(), UiClasses.ShelfCard.Badge));
                }
                else if (def.UnlockFish > 0 && def.UnlockFish == IslandCatalog.UnlockSteps.Where(n => n <= island.MaxFishHeld).Max())
                {
                    // いちばん新しく開いた段の、まだ建てていない施設。
                    card.Add(IslandUi.Text("NEW", UiClasses.ShelfCard.New));
                }

                _shelf.Add(card);
            }

            var expand = Card(UiClasses.Thumb.Expand, "島を広げる", ExpandIndex);
            if (island.ExpandRoom > 0)
            {
                expand.Add(IslandUi.Text(island.ExpandRoom.ToString(), UiClasses.ShelfCard.Badge));
            }

            _shelf.Add(expand);
        }

        private VisualElement Card(string thumbClass, string name, int index)
        {
            var card = new VisualElement();
            card.AddToClassList(UiClasses.ShelfCard.Root);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(UiClasses.ShelfCard.Thumb);
            IslandUi.SetThumb(thumb, thumbClass);
            card.Add(thumb);
            card.Add(IslandUi.Text(name, UiClasses.ShelfCard.Name));
            card.RegisterCallback<ClickEvent>(_ => ShowDetail(index));
            return card;
        }

        /// <param name="fish">数の前にお魚のアイコンを付ける (お魚の桁で開く鍵)。</param>
        private static VisualElement LockMark(string label, bool fish = false)
        {
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList(UiClasses.Lock.Root);
            mark.Add(IslandUi.Icon(UiClasses.Icon.Lock, null, UiClasses.Icon.ExtraLarge));
            mark.Add(fish ? IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, label) : IslandUi.Text(label, UiClasses.Lock.Label));
            return mark;
        }

        // ------------------------------------------------------------------
        // 説明
        // ------------------------------------------------------------------

        private void ShowDetail(int index)
        {
            _index = Mathf.Clamp(index, 0, ExpandIndex);
            if (_navigator.Current == AppScreen.BuildDetail)
            {
                RefreshDetail();
            }
            else
            {
                _navigator.Show(AppScreen.BuildDetail);
            }
        }

        private void Step(int delta)
        {
            var next = Mathf.Clamp(_index + delta, 0, ExpandIndex);
            if (next == _index)
            {
                return;
            }

            _index = next;
            _mainCard.AddToClassList(UiClasses.PhotoCard.Swap);
            _mainCard.schedule.Execute(() => _mainCard.RemoveFromClassList(UiClasses.PhotoCard.Swap)).StartingIn(90);
            RefreshDetail();
        }

        private void OnPrev(ClickEvent e)
        {
            e.StopPropagation();
            Step(-1);
        }

        private void OnNext(ClickEvent e)
        {
            e.StopPropagation();
            Step(+1);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            _pointerDown = true;
            _pointerStart = e.position;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_pointerDown)
            {
                return;
            }

            _pointerDown = false;
            var dx = e.position.x - _pointerStart.x;
            if (Mathf.Abs(dx) >= SwipeThreshold)
            {
                Step(dx < 0 ? +1 : -1);
            }
        }

        private void OnPointerLeave(PointerLeaveEvent e) => _pointerDown = false;

        private void RefreshDetail(bool full = true)
        {
            var island = _game.Island;
            var p = island.Parameters;
            IslandUi.SetVisible(_prevCard, _index > 0);
            IslandUi.SetVisible(_nextCard, _index < ExpandIndex);
            if (_index > 0)
            {
                IslandUi.SetThumb(_prevImage, ThumbOf(_index - 1));
                _prevCard.EnableInClassList(UiClasses.PhotoCard.Locked, !IsUnlocked(_index - 1));
            }

            if (_index < ExpandIndex)
            {
                IslandUi.SetThumb(_nextImage, ThumbOf(_index + 1));
                _nextCard.EnableInClassList(UiClasses.PhotoCard.Locked, !IsUnlocked(_index + 1));
            }

            IslandUi.SetThumb(_mainImage, ThumbOf(_index));
            _costs.Clear();

            if (_index == ExpandIndex)
            {
                RefreshExpand(island);
                return;
            }

            var def = _defs[_index];
            var unlocked = island.IsUnlocked(def.Kind);
            _mainCard.EnableInClassList(UiClasses.PhotoCard.Locked, !unlocked);
            IslandUi.SetVisible(_mainLock, !unlocked);
            _mainLockLabel.text = IslandUi.UnlockText(def.UnlockFish);
            _name.text = def.Name;
            _lead.text = def.Description;

            var recipe = def.Role == FacilityRole.Producer && def.Recipes.Count > 0 ? def.Recipes[0] : null;
            IslandUi.SetVisible(_time, recipe != null);
            if (full)
            {
                if (recipe != null)
                {
                    _timeValue.text = $"{recipe.Seconds:0} 秒";
                    IslandUi.FillRecipe(_recipe, def.Kind, recipe);
                }
                else
                {
                    _recipe.Clear();
                    _recipe.Add(IslandUi.Count(IslandUi.EffectIcon(def), IslandUi.TintClass(def), IslandUi.EffectShort(def, p)));
                }
            }

            var hasElectricity = island.Get(Resource.Electricity) + island.Get(Resource.AllMighty);
            _costs.Add(IslandUi.Count(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, def.ElectricityCost, hasElectricity < def.ElectricityCost));
            if (def.WaterCost > 0)
            {
                var hasWater = island.Get(Resource.Water) + island.Get(Resource.AllMighty);
                _costs.Add(IslandUi.Count(UiClasses.Icon.Water, UiClasses.Tint.Water, def.WaterCost, hasWater < def.WaterCost));
            }

            if (def.FishCost > 0)
            {
                _costs.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, def.FishCost, island.Fish < def.FishCost));
            }

            if (def.FishPerHour > 0)
            {
                // 動かしている間ずっと、働くペンギンがお魚を食べる。
                _costs.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, $"{IslandUi.Rate(def.FishPerHour)}/時"));
            }

            if (def.Workers > 0)
            {
                _costs.Add(IslandUi.Count(UiClasses.Icon.Penguin, UiClasses.Tint.Original, def.Workers, island.FreeWorkers < def.Workers));
            }

            var count = island.CountOf(def.Kind);
            _owned.text = def.MaxCount == int.MaxValue ? $"島に {count} 棟" : $"島に {count} / {def.MaxCount} 棟";
            var can = island.CanBuild(def.Kind);
            _buildButton.SetEnabled(can == IslandActionResult.Ok);
            _buildLabel.text = can switch
            {
                IslandActionResult.Locked => "まだ",
                IslandActionResult.LimitReached => "上限",
                _ => "建てる",
            };
        }

        private void RefreshExpand(IslandGame island)
        {
            _mainCard.RemoveFromClassList(UiClasses.PhotoCard.Locked);
            IslandUi.SetVisible(_mainLock, false);
            IslandUi.SetVisible(_time, false);
            _name.text = "島を広げる";
            _lead.text = "光る海を凍らせて、陸を 1 マス増やす。氷が足りないと端から沈むので気をつけて";
            _recipe.Clear();
            _recipe.Add(IslandUi.Count(UiClasses.Icon.ExpandIsland, UiClasses.Tint.Primary, $"あと {island.ExpandRoom} マス"));
            _costs.Add(IslandUi.Count(UiClasses.Icon.Water, UiClasses.Tint.Water, island.ExpandWaterCost,
                island.Get(Resource.Water) + island.Get(Resource.AllMighty) < island.ExpandWaterCost));
            _costs.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, island.ExpandFishCost, island.Fish < island.ExpandFishCost));
            _owned.text = $"広げたマス {island.Board.Expansions.Count}";
            var check = ExpandCheck(island);
            _buildButton.SetEnabled(check == IslandActionResult.Ok);
            _buildLabel.text = check == IslandActionResult.NotEnoughIce ? "氷が足りない" : check == IslandActionResult.LimitReached ? "上限" : "広げる";
        }

        /// <summary>広げ始めてよいか (場所を選ぶ前に分かる理由だけ)。</summary>
        private static IslandActionResult ExpandCheck(IslandGame island)
        {
            if (island.Board.SunkCount(island.Ice) > 0 || island.Board.Expansions.Count >= island.Board.Capacity(island.Ice))
            {
                return IslandActionResult.NotEnoughIce;
            }

            return island.ExpandRoom == 0 ? IslandActionResult.LimitReached : IslandActionResult.Ok;
        }

        private string ThumbOf(int index) => index >= ExpandIndex ? UiClasses.Thumb.Expand : UiClasses.Thumb.Of(_defs[index].Kind);

        private bool IsUnlocked(int index) => index >= ExpandIndex || _game.Island.IsUnlocked(_defs[index].Kind);

        private void OnBuild()
        {
            if (_index == ExpandIndex)
            {
                _navigator.Show(AppScreen.Home);
                _sheets?.StartExpand();
                return;
            }

            var kind = _defs[_index].Kind;
            var can = _game.Island.CanBuild(kind);
            if (can != IslandActionResult.Ok)
            {
                _hud?.ShowToast(IslandUi.Message(can));
                return;
            }

            _navigator.Show(AppScreen.Home);
            _interaction.BeginPlace(kind);
        }
    }
}
