// 飲んだ水: 飲みきった空のペットボトルをその場でカメラで撮る (docs/decisions.md の工場ゲームの水)。C# 側は Assets/Scripts/Systems/WaterCameraSource.cs。
// 写真ライブラリからは選ばせない (古い写真やネットの画像を使い回せないようにする)。
// 撮った画像はメモリの中で JPEG にして C# に渡し、渡したら捨てる。ディスク・アルバム・ログには残さない (.claude/rules/privacy.md)。
// ログ (NSLog) に出すのは段と状態だけ。写真の中身は出さない。
import AVFoundation
import CryptoKit
import Foundation
import UIKit

private enum CaptureState: Int32 {
    case idle = 0
    case busy = 1         // 撮っている・縮めている
    case done = 2         // 結果あり
    case cancelled = 3    // 撮らずに閉じた
    case failed = 4       // 画像を作れない
    case unavailable = 5  // カメラが使えない (シミュレーターなど)
    case denied = 6       // カメラの許可がない (設定アプリで許可してもらう)
}

private final class PenguinWaterCamera: NSObject, UIImagePickerControllerDelegate, UINavigationControllerDelegate {
    static let shared = PenguinWaterCamera()

    private let lock = NSLock()
    private var state = CaptureState.idle
    private var result: String?
    private var steps: [(side: Int, quality: Int)] = []
    private var maxBytes = 0
    private let work = DispatchQueue(label: "penguin.watercamera", qos: .userInitiated)

    var currentState: Int32 {
        lock.lock(); defer { lock.unlock() }
        return state.rawValue
    }

    func takeResult() -> String? {
        lock.lock(); defer { lock.unlock() }
        let taken = result
        result = nil
        if state == .done { state = .idle }
        return taken
    }

    private func finish(_ newState: CaptureState, _ json: String? = nil) {
        NSLog("PenguinWaterCamera: finish %d", newState.rawValue)
        lock.lock()
        state = newState
        result = json
        lock.unlock()
    }

    func capture(steps: [(side: Int, quality: Int)], maxBytes: Int) {
        lock.lock()
        if state == .busy { lock.unlock(); return }
        state = .busy
        result = nil
        self.steps = steps
        self.maxBytes = maxBytes
        lock.unlock()

        DispatchQueue.main.async {
            guard UIImagePickerController.isSourceTypeAvailable(.camera) else {
                NSLog("PenguinWaterCamera: camera source unavailable")
                self.finish(.unavailable)
                return
            }
            switch AVCaptureDevice.authorizationStatus(for: .video) {
            case .authorized:
                self.present()
            case .notDetermined:
                // 初回は許可を尋ねてから開く。尋ねずに開くと、許可の前に真っ黒なカメラが出ることがある。
                NSLog("PenguinWaterCamera: requesting camera access")
                AVCaptureDevice.requestAccess(for: .video) { granted in
                    DispatchQueue.main.async {
                        if granted {
                            self.present()
                        } else {
                            self.finish(.denied)
                        }
                    }
                }
            default:
                NSLog("PenguinWaterCamera: camera access denied or restricted")
                self.finish(.denied)
            }
        }
    }

    private func present() {
        guard let presenter = PenguinWaterCamera.topViewController() else {
            NSLog("PenguinWaterCamera: no view controller to present from")
            finish(.failed)
            return
        }
        let picker = UIImagePickerController()
        picker.sourceType = .camera
        picker.cameraCaptureMode = .photo
        picker.allowsEditing = false
        // シートで出すと下に引いて閉じたときに delegate が呼ばれず、撮影中のまま止まる。全画面で出す。
        picker.modalPresentationStyle = .fullScreen
        picker.delegate = self
        picker.cameraOverlayView = Self.guideOverlay(frame: presenter.view.window?.bounds ?? UIScreen.main.bounds,
                                                     safeTop: presenter.view.window?.safeAreaInsets.top ?? 0)
        NSLog("PenguinWaterCamera: presenting camera")
        presenter.present(picker, animated: true)
    }

    /// 撮り方の案内の帯。容量はラベルの表示から読む (こちらで 350ml・500ml を決めない) ので、表示が写るように頼む。
    /// タッチは下のカメラの操作に素通しする。
    private static func guideOverlay(frame: CGRect, safeTop: CGFloat) -> UIView {
        let overlay = UIView(frame: frame)
        overlay.isUserInteractionEnabled = false
        overlay.backgroundColor = .clear
        overlay.autoresizingMask = [.flexibleWidth, .flexibleHeight]

        let label = UILabel()
        label.text = "飲みきったボトルを、ラベルの容量\n(500ml・2L など) が写るように撮ってね"
        label.numberOfLines = 2
        label.textAlignment = .center
        label.textColor = .white
        label.font = .systemFont(ofSize: 15, weight: .semibold)

        let band = UIView()
        band.backgroundColor = UIColor.black.withAlphaComponent(0.55)
        band.layer.cornerRadius = 14
        band.addSubview(label)
        overlay.addSubview(band)

        // カメラの上の操作 (フラッシュなど) の下に置く。
        let width = min(frame.width - 32, 360)
        let fit = label.sizeThatFits(CGSize(width: width - 24, height: .greatestFiniteMagnitude))
        band.frame = CGRect(x: (frame.width - width) / 2, y: safeTop + 64, width: width, height: fit.height + 20)
        band.autoresizingMask = [.flexibleLeftMargin, .flexibleRightMargin]
        label.frame = band.bounds.insetBy(dx: 12, dy: 10)
        return overlay
    }

    func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
        picker.dismiss(animated: true)
        finish(.cancelled)
    }

    func imagePickerController(_ picker: UIImagePickerController,
                               didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]) {
        picker.dismiss(animated: true)
        guard let image = info[.originalImage] as? UIImage else {
            NSLog("PenguinWaterCamera: no image in picker result")
            finish(.failed)
            return
        }
        NSLog("PenguinWaterCamera: captured, encoding")
        // UIImage はこの関数の中だけで持ち、縮めたら捨てる (プロパティに残さない)。
        work.async { self.encode(image) }
    }

    /// C# の PhotoFit.Steps と同じ順に、長い辺と品質を下げながら maxBytes に収める。
    private func encode(_ image: UIImage) {
        lock.lock()
        let steps = self.steps
        let maxBytes = self.maxBytes
        lock.unlock()

        var last: Data?
        for step in steps {
            guard let data = Self.jpeg(image, maxSide: step.side, quality: step.quality) else { break }
            last = data
            if data.count <= maxBytes { break }
        }
        guard let jpeg = last, jpeg.count <= maxBytes else {
            NSLog("PenguinWaterCamera: could not fit JPEG within the limit")
            finish(.failed)
            return
        }

        let hash = SHA256.hash(data: jpeg).map { String(format: "%02x", $0) }.joined()
        let payload: [String: Any] = ["hash": hash, "jpeg": jpeg.base64EncodedString()]
        guard let json = try? JSONSerialization.data(withJSONObject: payload),
              let text = String(data: json, encoding: .utf8) else {
            finish(.failed)
            return
        }
        finish(.done, text)
    }

    /// 向きを直して (画面に見えている向きにして) 長い辺を maxSide 以下に縮め、JPEG にする。
    private static func jpeg(_ image: UIImage, maxSide: Int, quality: Int) -> Data? {
        let size = image.size
        let longest = max(size.width, size.height)
        guard longest > 0 else { return nil }
        let scale = min(1, CGFloat(maxSide) / longest)
        let target = CGSize(width: floor(size.width * scale), height: floor(size.height * scale))
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1
        format.opaque = true
        let resized = UIGraphicsImageRenderer(size: target, format: format).image { _ in
            image.draw(in: CGRect(origin: .zero, size: target))
        }
        return resized.jpegData(compressionQuality: CGFloat(quality) / 100)
    }

    /// Unity の画面 (表示中の一番上の画面)。
    static func topViewController() -> UIViewController? {
        let window = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap { $0.windows }
            .first { $0.isKeyWindow }
        var top = window?.rootViewController
        while let presented = top?.presentedViewController {
            top = presented
        }
        return top
    }
}

/// カメラを開いて 1 枚撮る。sides と qualities は縮め直す段 (同じ長さ count)、maxBytes は JPEG の上限。
@_cdecl("PenguinWaterCamera_Capture")
public func PenguinWaterCamera_Capture(_ sides: UnsafePointer<Int32>?, _ qualities: UnsafePointer<Int32>?,
                                       _ count: Int32, _ maxBytes: Int32) {
    guard let sides = sides, let qualities = qualities, count > 0 else { return }
    let steps = (0..<Int(count)).map { (side: Int(sides[$0]), quality: Int(qualities[$0])) }
    PenguinWaterCamera.shared.capture(steps: steps, maxBytes: Int(maxBytes))
}

/// 0: 待機, 1: 撮影・処理中, 2: 結果あり, 3: キャンセル, 4: 失敗, 5: カメラが使えない, 6: カメラの許可がない
@_cdecl("PenguinWaterCamera_State")
public func PenguinWaterCamera_State() -> Int32 {
    PenguinWaterCamera.shared.currentState
}

/// 結果の JSON ({hash, jpeg (base64)})。無ければ NULL。返した文字列は IL2CPP が解放する。
@_cdecl("PenguinWaterCamera_TakeResult")
public func PenguinWaterCamera_TakeResult() -> UnsafeMutablePointer<CChar>? {
    guard let json = PenguinWaterCamera.shared.takeResult() else { return nil }
    return strdup(json)
}
