// 歩数を CoreMotion (CMPedometer) から読む。C# 側は Assets/Scripts/Systems/StepSource.cs。
// HealthKit は使わない (権限とエンタイトルメントが重いため)。端末の外には何も送らない。
#import <CoreMotion/CoreMotion.h>
#import <stdatomic.h>

static CMPedometer *penguinPedometer;
static atomic_int penguinState = 0;   // 0: 未要求, 1: 問い合わせ中, 2: 完了, 3: 失敗・許可なし
static atomic_int penguinSteps = -1;

// 問い合わせが重なったとき (日付が変わる直前に出したものの応答が後から届くなど)、
// 最後に出した問い合わせの応答だけを結果にする。結果には、その問い合わせの開始時刻 (UNIX ミリ秒) を添える。
static atomic_int penguinSerial = 0;
static atomic_llong penguinResultFromMs = 0;

extern "C" {

bool PenguinPedometer_IsAvailable(void)
{
    return [CMPedometer isStepCountingAvailable];
}

void PenguinPedometer_Query(double fromUnixSeconds)
{
    if (![CMPedometer isStepCountingAvailable]) {
        atomic_store(&penguinState, 3);
        return;
    }

    if (penguinPedometer == nil) {
        penguinPedometer = [[CMPedometer alloc] init];
    }

    int serial = atomic_fetch_add(&penguinSerial, 1) + 1;
    long long fromMs = (long long)llround(fromUnixSeconds * 1000.0);
    atomic_store(&penguinState, 1);
    NSDate *from = [NSDate dateWithTimeIntervalSince1970:fromUnixSeconds];
    [penguinPedometer queryPedometerDataFromDate:from toDate:[NSDate date] withHandler:^(CMPedometerData *data, NSError *error) {
        if (atomic_load(&penguinSerial) != serial) {
            return;   // 後から出した問い合わせがある。そちらの応答を待つ
        }

        if (error != nil || data == nil) {
            atomic_store(&penguinState, 3);
            return;
        }

        atomic_store(&penguinSteps, data.numberOfSteps.intValue);
        atomic_store(&penguinResultFromMs, fromMs);
        atomic_store(&penguinState, 2);
    }];
}

int PenguinPedometer_State(void)
{
    return atomic_load(&penguinState);
}

int PenguinPedometer_Steps(void)
{
    return atomic_load(&penguinSteps);
}

// 結果を求めた問い合わせの開始時刻 (UNIX 秒)。
double PenguinPedometer_ResultFrom(void)
{
    return atomic_load(&penguinResultFromMs) / 1000.0;
}

}
