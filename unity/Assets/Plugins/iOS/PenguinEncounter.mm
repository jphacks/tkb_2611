// すれ違いの BLE (CoreBluetooth)。C# 側は Assets/Scripts/Systems/EncounterRadio.cs。
// 自分のトークン (22 文字の乱数) を GATT の読み取り用の characteristic で出し、同じ service を広告する。
// 近くの端末を同じ service で走査し、つないでトークンを読んだら切る。読んだトークンはキューに積み、C# が取り出す。
// アプリが前面にあるときだけ動く (バックグラウンドのモードは足していない)。トークンはログに出さない。
#import <CoreBluetooth/CoreBluetooth.h>
#import <stdatomic.h>

// この試作だけの service と characteristic (乱数で作った UUID)。
static NSString *const PenguinServiceUUID = @"5B3962FF-90F1-4FB6-A275-FDFE22B236E7";
static NSString *const PenguinTokenUUID = @"EB1AA7B7-4BA8-4BC8-AFDE-8089CDD562B6";
static const NSUInteger PenguinTokenLength = 22;
// 同じ端末を読み直すまでの秒数と、つなぎに行って返事が無いときにあきらめる秒数。
static const NSTimeInterval PenguinRereadSeconds = 60;
static const NSTimeInterval PenguinConnectTimeoutSeconds = 10;
static const NSUInteger PenguinMaxConnections = 4;
static const NSUInteger PenguinMaxQueuedPeers = 64;

// 0: 止まっている, 1: 始めている, 2: 動いている, 3: 許可なし, 4: 電源が切れている, 5: 非対応 (EncounterRadio.cs と同じ)
static atomic_int penguinEncounterState = 0;

@interface PenguinEncounterRadio : NSObject <CBPeripheralManagerDelegate, CBCentralManagerDelegate, CBPeripheralDelegate>
@end

@implementation PenguinEncounterRadio
{
    dispatch_queue_t _queue;
    CBPeripheralManager *_peripheralManager;
    CBCentralManager *_centralManager;
    CBMutableCharacteristic *_characteristic;
    NSData *_token;
    BOOL _running;
    BOOL _serviceAdded;
    NSMutableDictionary<NSUUID *, CBPeripheral *> *_connecting;
    NSMutableDictionary<NSUUID *, NSDate *> *_lastRead;
    NSMutableArray<NSString *> *_peers;
}

+ (instancetype)shared
{
    static PenguinEncounterRadio *radio;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ radio = [[PenguinEncounterRadio alloc] init]; });
    return radio;
}

- (instancetype)init
{
    if ((self = [super init])) {
        _queue = dispatch_queue_create("ueco.penguin.encounter", DISPATCH_QUEUE_SERIAL);
        _connecting = [NSMutableDictionary dictionary];
        _lastRead = [NSMutableDictionary dictionary];
        _peers = [NSMutableArray array];
    }
    return self;
}

- (void)startWithToken:(NSData *)token
{
    dispatch_async(_queue, ^{
        self->_token = token;
        if (self->_running) {
            return;   // 動いている最中はトークンだけを差し替える (読み取りの応答は _token を返す)
        }
        self->_running = YES;
        atomic_store(&penguinEncounterState, 1);
        // 2 つ目以降は作り直さない (許可の確認が出るのは最初の 1 回だけ)。作った時点で状態の通知が来る。
        if (self->_peripheralManager == nil) {
            self->_peripheralManager = [[CBPeripheralManager alloc] initWithDelegate:self queue:self->_queue];
        } else {
            [self peripheralManagerDidUpdateState:self->_peripheralManager];
        }
        if (self->_centralManager == nil) {
            self->_centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:self->_queue];
        } else {
            [self centralManagerDidUpdateState:self->_centralManager];
        }
    });
}

- (void)stop
{
    dispatch_async(_queue, ^{
        self->_running = NO;
        if (self->_peripheralManager.state == CBManagerStatePoweredOn) {
            [self->_peripheralManager stopAdvertising];
            [self->_peripheralManager removeAllServices];
        }
        self->_serviceAdded = NO;
        if (self->_centralManager.state == CBManagerStatePoweredOn) {
            [self->_centralManager stopScan];
            for (CBPeripheral *peripheral in self->_connecting.allValues) {
                [self->_centralManager cancelPeripheralConnection:peripheral];
            }
        }
        [self->_connecting removeAllObjects];
        @synchronized (self->_peers) {
            [self->_peers removeAllObjects];
        }
        atomic_store(&penguinEncounterState, 0);
    });
}

- (int)takePeer:(char *)buffer length:(int)length
{
    NSString *peer = nil;
    @synchronized (_peers) {
        if (_peers.count > 0) {
            peer = _peers.firstObject;
            [_peers removeObjectAtIndex:0];
        }
    }
    if (peer == nil) {
        return 0;
    }
    NSData *bytes = [peer dataUsingEncoding:NSASCIIStringEncoding];
    if (bytes.length > (NSUInteger)length) {
        return 0;
    }
    memcpy(buffer, bytes.bytes, bytes.length);
    return (int)bytes.length;
}

// 両方の役が使えるときだけ「動いている」。どちらかの失敗の状態を優先して出す。
- (void)updateState
{
    if (!_running) {
        return;
    }
    CBManagerState states[2] = { _peripheralManager.state, _centralManager.state };
    for (int i = 0; i < 2; i++) {
        if (states[i] == CBManagerStateUnauthorized) { atomic_store(&penguinEncounterState, 3); return; }
        if (states[i] == CBManagerStatePoweredOff) { atomic_store(&penguinEncounterState, 4); return; }
        if (states[i] == CBManagerStateUnsupported) { atomic_store(&penguinEncounterState, 5); return; }
    }
    BOOL on = states[0] == CBManagerStatePoweredOn && states[1] == CBManagerStatePoweredOn;
    atomic_store(&penguinEncounterState, on ? 2 : 1);
}

#pragma mark - 広告する側 (Peripheral)

- (void)peripheralManagerDidUpdateState:(CBPeripheralManager *)peripheral
{
    [self updateState];
    if (!_running || peripheral.state != CBManagerStatePoweredOn) {
        _serviceAdded = NO;
        return;
    }
    if (!_serviceAdded) {
        // 値を nil にすると、読み取りのたびに didReceiveReadRequest で今のトークンを返せる。
        _characteristic = [[CBMutableCharacteristic alloc] initWithType:[CBUUID UUIDWithString:PenguinTokenUUID]
                                                             properties:CBCharacteristicPropertyRead
                                                                  value:nil
                                                            permissions:CBAttributePermissionsReadable];
        CBMutableService *service = [[CBMutableService alloc] initWithType:[CBUUID UUIDWithString:PenguinServiceUUID] primary:YES];
        service.characteristics = @[ _characteristic ];
        [peripheral removeAllServices];
        [peripheral addService:service];
        _serviceAdded = YES;
    }
}

- (void)peripheralManager:(CBPeripheralManager *)peripheral didAddService:(CBService *)service error:(NSError *)error
{
    if (error != nil || !_running) {
        _serviceAdded = NO;
        return;
    }
    [peripheral startAdvertising:@{ CBAdvertisementDataServiceUUIDsKey: @[ [CBUUID UUIDWithString:PenguinServiceUUID] ] }];
}

- (void)peripheralManager:(CBPeripheralManager *)peripheral didReceiveReadRequest:(CBATTRequest *)request
{
    NSData *token = _token;
    if (![request.characteristic.UUID isEqual:[CBUUID UUIDWithString:PenguinTokenUUID]] || token == nil) {
        [peripheral respondToRequest:request withResult:CBATTErrorReadNotPermitted];
        return;
    }
    if (request.offset > token.length) {
        [peripheral respondToRequest:request withResult:CBATTErrorInvalidOffset];
        return;
    }
    request.value = [token subdataWithRange:NSMakeRange(request.offset, token.length - request.offset)];
    [peripheral respondToRequest:request withResult:CBATTErrorSuccess];
}

#pragma mark - 走査する側 (Central)

- (void)centralManagerDidUpdateState:(CBCentralManager *)central
{
    [self updateState];
    if (!_running || central.state != CBManagerStatePoweredOn) {
        return;
    }
    // 同じ端末の広告も受け取り、読み直しの間隔は _lastRead で自分で決める。
    [central scanForPeripheralsWithServices:@[ [CBUUID UUIDWithString:PenguinServiceUUID] ]
                                    options:@{ CBCentralManagerScanOptionAllowDuplicatesKey: @YES }];
}

- (void)centralManager:(CBCentralManager *)central didDiscoverPeripheral:(CBPeripheral *)peripheral
     advertisementData:(NSDictionary<NSString *, id> *)advertisementData RSSI:(NSNumber *)RSSI
{
    NSUUID *identifier = peripheral.identifier;
    NSDate *last = _lastRead[identifier];
    if (!_running || _connecting[identifier] != nil || _connecting.count >= PenguinMaxConnections ||
        (last != nil && -[last timeIntervalSinceNow] < PenguinRereadSeconds)) {
        return;
    }
    _connecting[identifier] = peripheral;   // つないでいる間は参照を持っておく (持たないと切れる)
    peripheral.delegate = self;
    [central connectPeripheral:peripheral options:nil];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(PenguinConnectTimeoutSeconds * NSEC_PER_SEC)), _queue, ^{
        if (self->_connecting[identifier] == peripheral) {
            [self finish:peripheral];
        }
    });
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral
{
    [peripheral discoverServices:@[ [CBUUID UUIDWithString:PenguinServiceUUID] ]];
}

- (void)centralManager:(CBCentralManager *)central didFailToConnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error
{
    [self finish:peripheral];
}

- (void)centralManager:(CBCentralManager *)central didDisconnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error
{
    [_connecting removeObjectForKey:peripheral.identifier];
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverServices:(NSError *)error
{
    for (CBService *service in peripheral.services) {
        if ([service.UUID isEqual:[CBUUID UUIDWithString:PenguinServiceUUID]]) {
            [peripheral discoverCharacteristics:@[ [CBUUID UUIDWithString:PenguinTokenUUID] ] forService:service];
            return;
        }
    }
    [self finish:peripheral];
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverCharacteristicsForService:(CBService *)service error:(NSError *)error
{
    for (CBCharacteristic *characteristic in service.characteristics) {
        if ([characteristic.UUID isEqual:[CBUUID UUIDWithString:PenguinTokenUUID]]) {
            [peripheral readValueForCharacteristic:characteristic];
            return;
        }
    }
    [self finish:peripheral];
}

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error
{
    NSData *value = characteristic.value;
    if (error == nil && value.length == PenguinTokenLength) {
        NSString *token = [[NSString alloc] initWithData:value encoding:NSASCIIStringEncoding];
        if (token != nil) {
            @synchronized (_peers) {
                if (_peers.count < PenguinMaxQueuedPeers) {
                    [_peers addObject:token];
                }
            }
        }
    }
    [self finish:peripheral];
}

// 読めても読めなくても、同じ端末は PenguinRereadSeconds の間は読みに行かない。
- (void)finish:(CBPeripheral *)peripheral
{
    _lastRead[peripheral.identifier] = [NSDate date];
    [_connecting removeObjectForKey:peripheral.identifier];
    if (_centralManager.state == CBManagerStatePoweredOn) {
        [_centralManager cancelPeripheralConnection:peripheral];
    }
}

@end

extern "C" {

void PenguinEncounter_Start(const char *selfToken)
{
    if (selfToken == NULL || strlen(selfToken) != PenguinTokenLength) {
        return;
    }
    NSData *token = [NSData dataWithBytes:selfToken length:PenguinTokenLength];
    [[PenguinEncounterRadio shared] startWithToken:token];
}

void PenguinEncounter_Stop(void)
{
    [[PenguinEncounterRadio shared] stop];
}

int PenguinEncounter_State(void)
{
    return atomic_load(&penguinEncounterState);
}

int PenguinEncounter_TakePeer(char *buffer, int length)
{
    if (buffer == NULL || length <= 0) {
        return 0;
    }
    return [[PenguinEncounterRadio shared] takePeer:buffer length:length];
}

}
