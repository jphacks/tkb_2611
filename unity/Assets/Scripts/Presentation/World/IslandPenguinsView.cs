using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 手のあいているペンギン (配属されていない働き手) を島の上で歩かせる。
    /// 記録を続けるほど住むペンギンが増えるのを、島の上のにぎやかさで見せるため。
    /// 歩く先は沈んでいない陸のうち施設の無いマス。足元が沈んだら基礎の島へ戻る。
    /// </summary>
    public sealed class IslandPenguinsView : MonoBehaviour
    {
        private sealed class Walker
        {
            public Transform Transform;
            public Vector3 Target;
            public float Wait;

            /// <summary>足踏みのアニメーション (雛形が持っていれば)。</summary>
            public Animator Animator;
        }

        /// <summary>立ち止まっている間の足踏みの速さ (歩いている間は 1)。その場でゆっくり足を動かす。</summary>
        private const float IdleWaddleSpeed = 0.35f;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private IslandBoardView _board;

        [Tooltip("歩き方付きのペンギン (複製して使う)")]
        [SerializeField] private GameObject _template;

        [SerializeField] private int _maxPenguins = 10;

        [Tooltip("いつも出しておく最低の数 (島が寂しくならないように)")]
        [SerializeField] private int _minPenguins = 1;

        [SerializeField] private float _scale = 0.62f;
        [SerializeField] private float _speed = 0.35f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private Transform _root;

        private void Awake()
        {
            _root = new GameObject("Penguins").transform;
            _root.SetParent(_board != null ? _board.transform : transform, false);
        }

        private void OnEnable()
        {
            if (_game == null || _board == null || _template == null)
            {
                Debug.LogError($"{nameof(IslandPenguinsView)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _game.IslandChanged += Sync;
            Sync();
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.IslandChanged -= Sync;
            }
        }

        private void Sync()
        {
            var want = Mathf.Clamp(_game.Island.FreeWorkers, _minPenguins, _maxPenguins);
            while (_walkers.Count < want)
            {
                Spawn();
            }

            while (_walkers.Count > want)
            {
                var w = _walkers[_walkers.Count - 1];
                _walkers.RemoveAt(_walkers.Count - 1);
                Destroy(w.Transform.gameObject);
            }
        }

        private void Spawn()
        {
            var go = Instantiate(_template, _root);
            go.name = "Penguin";
            foreach (var walker in go.GetComponentsInChildren<PenguinCircleWalker>(true))
            {
                Destroy(walker);
            }

            go.SetActive(true);
            go.transform.localScale = Vector3.one * _scale;
            var start = RandomLandPoint();
            go.transform.localPosition = start;
            IslandPop.Play(go.transform);
            _walkers.Add(new Walker
            {
                Transform = go.transform, Target = start, Wait = Random.Range(0f, 2f), Animator = go.GetComponentInChildren<Animator>(),
            });
        }

        private void Update()
        {
            var island = _game.Island;
            foreach (var w in _walkers)
            {
                var t = w.Transform;
                var here = IslandGrid.CellAt(t.localPosition, island.Board.Size);
                if (!island.Board.IsSolidLand(here, island.Ice))
                {
                    // 足元が沈んだ。基礎の島へ戻る。
                    t.localPosition = RandomLandPoint(baseOnly: true);
                    w.Target = t.localPosition;
                }

                if (w.Wait > 0f)
                {
                    w.Wait -= Time.deltaTime;
                    SetWaddle(w, IdleWaddleSpeed);
                    continue;
                }

                var to = w.Target - t.localPosition;
                to.y = 0f;
                if (to.magnitude < 0.05f)
                {
                    w.Target = RandomLandPoint();
                    w.Wait = Random.Range(0.8f, 3f);
                    continue;
                }

                SetWaddle(w, 1f);

                var step = Mathf.Min(to.magnitude, _speed * Time.deltaTime);
                t.localPosition += to.normalized * step;
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                t.localRotation = Quaternion.Slerp(t.localRotation, look, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        private static void SetWaddle(Walker w, float speed)
        {
            if (w.Animator != null)
            {
                w.Animator.speed = speed;
            }
        }

        /// <summary>沈んでいない陸で、施設の無いマスのどこか。</summary>
        private Vector3 RandomLandPoint(bool baseOnly = false)
        {
            var island = _game.Island;
            var board = island.Board;
            var cells = new List<Cell>();
            for (var x = 0; x < board.Size; x++)
            {
                for (var y = 0; y < board.Size; y++)
                {
                    var c = new Cell(x, y);
                    var land = baseOnly ? board.KindAt(c) == TileKind.Base : board.IsSolidLand(c, island.Ice);
                    if (land && island.FacilityAt(c) == null)
                    {
                        cells.Add(c);
                    }
                }
            }

            if (cells.Count == 0)
            {
                cells.AddRange(Enumerable.Range(0, board.Size * board.Size)
                    .Select(i => new Cell(i % board.Size, i / board.Size)).Where(c => board.KindAt(c) == TileKind.Base));
            }

            var cell = cells[Random.Range(0, cells.Count)];
            return IslandGrid.CellCenter(cell, board.Size) + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        }
    }
}
