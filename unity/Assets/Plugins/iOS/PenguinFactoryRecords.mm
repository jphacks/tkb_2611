// Local-only screen-time input. Images and OCR text stay in memory and are never sent to C# or written to disk.
#import <PhotosUI/PhotosUI.h>
#import <Vision/Vision.h>
#import <UIKit/UIKit.h>
#import "PenguinFactoryScreenMinutes.h"

extern "C" UIViewController *UnityGetGLViewController(void);
extern "C" void UnitySendMessage(const char *, const char *, const char *);

@interface PenguinFactoryRecords : UIViewController <PHPickerViewControllerDelegate>
@property(nonatomic, copy) NSString *target;
@property(nonatomic, copy) NSString *method;
@property(nonatomic, copy) NSString *expectedDate;
@property(nonatomic) int requestId;
@property(nonatomic) BOOL finished;
@end

static PenguinFactoryRecords *activeFactoryInput;

@implementation PenguinFactoryRecords
- (void)finish:(NSString *)status extra:(NSDictionary *)extra
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.finished) return;
        self.finished = YES;
        NSMutableDictionary *result = [NSMutableDictionary dictionaryWithDictionary:extra ?: @{}];
        result[@"status"] = status; result[@"requestId"] = @(self.requestId);
        NSData *data = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
        NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        [self dismissViewControllerAnimated:YES completion:^{
            UnitySendMessage(self.target.UTF8String, self.method.UTF8String, json.UTF8String);
            if (activeFactoryInput == self) activeFactoryInput = nil;
        }];
    });
}
- (void)cancel { [self finish:@"cancelled" extra:nil]; }
- (void)viewDidLoad
{
    [super viewDidLoad]; self.view.backgroundColor = UIColor.blackColor;
    UIButton *cancel = [UIButton buttonWithType:UIButtonTypeSystem];
    [cancel setTitle:@"取消" forState:UIControlStateNormal];
    cancel.backgroundColor = UIColor.whiteColor;
    cancel.frame = CGRectMake(24, 60, 100, 56);
    [cancel addTarget:self action:@selector(cancel) forControlEvents:UIControlEventTouchUpInside];
    [self.view addSubview:cancel];
}
- (void)viewDidDisappear:(BOOL)animated
{
    [super viewDidDisappear:animated];
    if (!self.finished && self.isBeingDismissed) [self finish:@"cancelled" extra:nil];
}
- (void)startScreenPicker
{
    if (self.finished) return;
    PHPickerConfiguration *configuration = [[PHPickerConfiguration alloc] init];
    configuration.filter = PHPickerFilter.imagesFilter; configuration.selectionLimit = 1;
    PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:configuration];
    picker.delegate = self; [self presentViewController:picker animated:YES completion:nil];
}
- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results
{
    [picker dismissViewControllerAnimated:YES completion:nil];
    if (results.count == 0) { [self finish:@"cancelled" extra:nil]; return; }
    NSItemProvider *provider = results[0].itemProvider;
    if (![provider canLoadObjectOfClass:UIImage.class]) { [self finish:@"invalid" extra:nil]; return; }
    [provider loadObjectOfClass:UIImage.class completionHandler:^(id<NSItemProviderReading> object, NSError *error) {
        @autoreleasepool {
            if (self.finished) return;
            if (error || ![(NSObject *)object isKindOfClass:UIImage.class]) { [self finish:@"invalid" extra:nil]; return; }
            UIImage *image = (UIImage *)object;
            if (!image.CGImage || image.size.width * image.size.height > 40000000) { [self finish:@"invalid" extra:nil]; return; }
            VNRecognizeTextRequest *request = [[VNRecognizeTextRequest alloc] init];
            request.recognitionLevel = VNRequestTextRecognitionLevelAccurate;
            request.recognitionLanguages = @[@"ja-JP", @"en-US"];
            request.usesLanguageCorrection = NO;
            // No image, full OCR text, or provider metadata crosses the plugin boundary.
            VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithCGImage:image.CGImage options:@{}];
            NSError *visionError;
            if (![handler performRequests:@[request] error:&visionError]) { [self finish:@"unreadable" extra:nil]; return; }
            NSMutableArray<NSString *> *lines = [NSMutableArray array];
            for (VNRecognizedTextObservation *observation in request.results)
            { NSString *line = [observation topCandidates:1].firstObject.string; if (line) [lines addObject:line]; }
            NSString *text = [lines componentsJoinedByString:@"\n"];
            BOOL screen = [text containsString:@"スクリーンタイム"] || [text.lowercaseString containsString:@"screen time"];
            NSArray<NSString *> *parts = [self.expectedDate componentsSeparatedByString:@"-"];
            NSString *japaneseDate = parts.count == 3 ? [NSString stringWithFormat:@"%ld月%ld日", (long)parts[1].integerValue, (long)parts[2].integerValue] : @"never-match";
            NSString *shortDate = parts.count == 3 ? [NSString stringWithFormat:@"%ld/%ld", (long)parts[1].integerValue, (long)parts[2].integerValue] : @"never-match";
            BOOL dateMatched = [text containsString:japaneseDate] || [text containsString:shortDate] || [text containsString:@"昨日"];
            NSInteger minutes = PenguinFactoryScreenMinutes(text);
            if (!screen || !dateMatched || minutes < 0) { [self finish:@"unreadable" extra:nil]; return; }
            [self finish:@"ok" extra:@{@"minutes": @(minutes)}];
        }
    }];
}
@end

extern "C" void PenguinFactoryRecords_Open(const char *target, const char *method, int requestId, const char *date)
{
    NSString *targetName = [NSString stringWithUTF8String:target];
    NSString *methodName = [NSString stringWithUTF8String:method];
    NSString *expected = [NSString stringWithUTF8String:date];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (activeFactoryInput != nil) return;
        PenguinFactoryRecords *controller = [[PenguinFactoryRecords alloc] init];
        controller.target = targetName; controller.method = methodName; controller.expectedDate = expected; controller.requestId = requestId;
        controller.modalPresentationStyle = UIModalPresentationFullScreen; activeFactoryInput = controller;
        [UnityGetGLViewController() presentViewController:controller animated:YES completion:^{
            [controller startScreenPicker];
        }];
    });
}

extern "C" void PenguinFactoryRecords_Cancel(int requestId)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (activeFactoryInput && activeFactoryInput.requestId == requestId) [activeFactoryInput cancel];
    });
}
