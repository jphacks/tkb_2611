// スクリーンタイムの画面 (「日」の表示) のスクショを 1 枚選んでもらい、端末の中で文字を読む (project-penguin の docs/adr/0017-screen-time-on-free-team.md)。
// C# 側は Assets/Scripts/Systems/ScreenTimeSource.cs。行の解析 (日付と合計) は C# の ScreenTimeOcrParser が行う。
// 画像はメモリの中だけで扱い、結果を作ったら捨てる。読んだ文字はログに出さない (.claude/rules/privacy.md)。
import CryptoKit
import Foundation
import PhotosUI
import UIKit
import UniformTypeIdentifiers
import Vision

private enum PickState: Int32 {
    case idle = 0
    case busy = 1       // 選んでいる・読んでいる
    case done = 2       // 結果あり
    case cancelled = 3  // 選ばずに閉じた
    case failed = 4     // 画像を読めない
}

private final class PenguinScreenTimePicker: NSObject, PHPickerViewControllerDelegate {
    static let shared = PenguinScreenTimePicker()

    private let lock = NSLock()
    private var state = PickState.idle
    private var result: String?
    private let work = DispatchQueue(label: "penguin.screentime", qos: .userInitiated)

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

    private func finish(_ newState: PickState, _ json: String? = nil) {
        lock.lock()
        state = newState
        result = json
        lock.unlock()
    }

    func pick() {
        lock.lock()
        if state == .busy { lock.unlock(); return }
        state = .busy
        result = nil
        lock.unlock()

        DispatchQueue.main.async {
            guard let presenter = Self.topViewController() else {
                self.finish(.failed)
                return
            }
            var config = PHPickerConfiguration()
            // スクリーンショットだけを出す。普通の写真を選んで読み違えるのを防ぐ (選べる候補もぐっと減る)。
            config.filter = .screenshots
            config.selectionLimit = 1
            let picker = PHPickerViewController(configuration: config)
            picker.delegate = self
            presenter.present(picker, animated: true)
        }
    }

    func picker(_ picker: PHPickerViewController, didFinishPicking results: [PHPickerResult]) {
        picker.dismiss(animated: true)
        guard let provider = results.first?.itemProvider,
              provider.hasItemConformingToTypeIdentifier(UTType.image.identifier) else {
            finish(.cancelled)
            return
        }
        provider.loadDataRepresentation(forTypeIdentifier: UTType.image.identifier) { data, _ in
            guard let data = data else {
                self.finish(.failed)
                return
            }
            self.work.async { self.process(data) }
        }
    }

    private func process(_ data: Data) {
        // 同じ画像の取り込みを弾くためのハッシュ (画像そのものは残さない)。
        let hash = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
        guard let image = UIImage(data: data), let cgImage = image.cgImage else {
            finish(.failed)
            return
        }

        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.recognitionLanguages = ["ja-JP", "en-US"]
        // 「6時間20分」の数字を言葉の補正で変えられないよう、補正は切る。
        request.usesLanguageCorrection = false

        let handler = VNImageRequestHandler(cgImage: cgImage, orientation: Self.orientation(image.imageOrientation))
        do {
            try handler.perform([request])
        } catch {
            finish(.failed)
            return
        }

        var lines: [[String: Any]] = []
        for observation in request.results ?? [] {
            guard let text = observation.topCandidates(1).first?.string else { continue }
            // Vision は左下が原点。C# 側に合わせて左上を原点にする。
            let box = observation.boundingBox
            lines.append([
                "text": text,
                "x": box.minX,
                "y": 1.0 - box.maxY,
                "w": box.width,
                "h": box.height,
            ])
        }

        let payload: [String: Any] = ["hash": hash, "lines": lines]
        guard let json = try? JSONSerialization.data(withJSONObject: payload),
              let text = String(data: json, encoding: .utf8) else {
            finish(.failed)
            return
        }
        finish(.done, text)
    }

    private static func orientation(_ o: UIImage.Orientation) -> CGImagePropertyOrientation {
        switch o {
        case .up: return .up
        case .down: return .down
        case .left: return .left
        case .right: return .right
        case .upMirrored: return .upMirrored
        case .downMirrored: return .downMirrored
        case .leftMirrored: return .leftMirrored
        case .rightMirrored: return .rightMirrored
        @unknown default: return .up
        }
    }

    /// Unity の画面 (表示中の一番上の画面)。UnityGetGLViewController は Swift から呼べないので、key window から辿る。
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

@_cdecl("PenguinScreenTime_Pick")
public func PenguinScreenTime_Pick() {
    PenguinScreenTimePicker.shared.pick()
}

/// 0: 待機, 1: 選択・処理中, 2: 結果あり, 3: キャンセル, 4: 失敗
@_cdecl("PenguinScreenTime_State")
public func PenguinScreenTime_State() -> Int32 {
    PenguinScreenTimePicker.shared.currentState
}

/// 結果の JSON ({hash, lines:[{text,x,y,w,h}]})。無ければ NULL。返した文字列は IL2CPP が解放する。
@_cdecl("PenguinScreenTime_TakeResult")
public func PenguinScreenTime_TakeResult() -> UnsafeMutablePointer<CChar>? {
    guard let json = PenguinScreenTimePicker.shared.takeResult() else { return nil }
    return strdup(json)
}
