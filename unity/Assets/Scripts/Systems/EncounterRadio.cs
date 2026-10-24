using System.Collections.Generic;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace ProjectPenguin.Systems
{
    /// <summary>すれ違いの BLE の状態。</summary>
    public enum EncounterRadioState
    {
        Off,
        /// <summary>Bluetooth の許可を求めている、または電源の状態を待っている。</summary>
        Starting,
        Running,
        /// <summary>Bluetooth の利用を許可されていない。</summary>
        Unauthorized,
        PoweredOff,
        Unsupported,
    }

    /// <summary>
    /// すれ違いの BLE の窓口。自分のトークンを広告し、近くの端末のトークンを読む。
    /// 読んだトークンは <see cref="TryTakePeer"/> で 1 つずつ受け取る。アプリが前面にあるときだけ動く。
    /// </summary>
    public interface IEncounterRadio
    {
        EncounterRadioState State { get; }

        /// <summary>広告と走査を始める。動いている最中に呼ぶと、広告するトークンを差し替える。</summary>
        void Start(string selfToken);

        void Stop();

        bool TryTakePeer(out string peerToken);
    }

    public static class EncounterRadios
    {
        /// <summary>実行環境に合った窓口。</summary>
        public static IEncounterRadio Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosEncounterRadio();
#else
            return new FakeEncounterRadio();
#endif
        }
    }

    /// <summary>Editor と iOS 以外。開発用の操作で、見つけた相手のトークンを足せる。</summary>
    public sealed class FakeEncounterRadio : IEncounterRadio
    {
        private readonly Queue<string> _peers = new Queue<string>();

        public EncounterRadioState State { get; private set; } = EncounterRadioState.Off;

        public void Start(string selfToken) => State = EncounterRadioState.Running;

        public void Stop()
        {
            State = EncounterRadioState.Off;
            _peers.Clear();
        }

        /// <summary>相手を見つけたことにする。止まっているときは何もしない。</summary>
        public void Inject(string peerToken)
        {
            if (State == EncounterRadioState.Running)
            {
                _peers.Enqueue(peerToken);
            }
        }

        public bool TryTakePeer(out string peerToken)
        {
            if (_peers.Count > 0)
            {
                peerToken = _peers.Dequeue();
                return true;
            }

            peerToken = null;
            return false;
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>iOS の CoreBluetooth (Assets/Plugins/iOS/PenguinEncounter.mm)。</summary>
    public sealed class IosEncounterRadio : IEncounterRadio
    {
        [DllImport("__Internal")]
        private static extern void PenguinEncounter_Start(string selfToken);

        [DllImport("__Internal")]
        private static extern void PenguinEncounter_Stop();

        [DllImport("__Internal")]
        private static extern int PenguinEncounter_State();

        [DllImport("__Internal")]
        private static extern int PenguinEncounter_TakePeer(byte[] buffer, int length);

        private readonly byte[] _buffer = new byte[64];

        // ネイティブの状態の番号 (PenguinEncounter.mm と同じ)。
        public EncounterRadioState State => PenguinEncounter_State() switch
        {
            1 => EncounterRadioState.Starting,
            2 => EncounterRadioState.Running,
            3 => EncounterRadioState.Unauthorized,
            4 => EncounterRadioState.PoweredOff,
            5 => EncounterRadioState.Unsupported,
            _ => EncounterRadioState.Off,
        };

        public void Start(string selfToken) => PenguinEncounter_Start(selfToken);

        public void Stop() => PenguinEncounter_Stop();

        public bool TryTakePeer(out string peerToken)
        {
            var n = PenguinEncounter_TakePeer(_buffer, _buffer.Length);
            peerToken = n > 0 ? Encoding.ASCII.GetString(_buffer, 0, n) : null;
            return n > 0;
        }
    }
#endif
}
