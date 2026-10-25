#pragma once
#import <Foundation/Foundation.h>

// Returns only a bounded candidate. The original OCR text never leaves native memory.
static inline NSInteger PenguinFactoryScreenMinutes(NSString *text)
{
    NSString *normalized = [text stringByApplyingTransform:NSStringTransformFullwidthToHalfwidth reverse:NO];
    NSString *pattern = @"(?<![0-9])([0-9]{1,2})\\s*(?:時間|hours?|hrs?|h)\\s*(?:([0-9]{1,2})\\s*(?:分|minutes?|mins?|m))?(?![a-z])|(?<![0-9])([0-9]{1,4})\\s*(?:分|minutes?|mins?|m)(?![a-z])";
    NSRegularExpression *duration = [NSRegularExpression regularExpressionWithPattern:pattern
        options:NSRegularExpressionCaseInsensitive error:nil];
    NSInteger candidate = -1;
    for (NSTextCheckingResult *match in [duration matchesInString:normalized options:0 range:NSMakeRange(0, normalized.length)])
    {
        NSRange hourRange = [match rangeAtIndex:1];
        NSInteger minutes;
        if (hourRange.location != NSNotFound)
        {
            NSInteger hours = [[normalized substringWithRange:hourRange] integerValue];
            NSRange minuteRange = [match rangeAtIndex:2];
            NSInteger remainder = minuteRange.location == NSNotFound ? 0 : [[normalized substringWithRange:minuteRange] integerValue];
            if (hours > 24 || remainder >= 60) continue;
            minutes = hours * 60 + remainder;
        }
        else minutes = [[normalized substringWithRange:[match rangeAtIndex:3]] integerValue];
        if (minutes <= 1440) candidate = MAX(candidate, minutes);
    }
    return candidate;
}
