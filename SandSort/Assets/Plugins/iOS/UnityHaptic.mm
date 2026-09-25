// Native side of MoowCore's HapticIOSInterface (Assets/Game/MoowCore/Runtime/Haptic Feedback).
// Type ids match Moow.HapticFeedbackType:
//   0 Light, 1 Medium, 2 Heavy, 3 Soft, 4 Rigid -> UIImpactFeedbackGenerator
//   5 Success, 6 Warning, 7 Error                -> UINotificationFeedbackGenerator
//   8 Selection                                  -> UISelectionFeedbackGenerator
//
// Phones whose hardware can't play those (CoreHaptics reports no haptics support, e.g. iPhone 6s /
// SE 1st gen) get the basic system vibration instead, rate limited because it is a long buzz and
// extraction pulses would otherwise chain it into a rumble. Devices with no vibration motor at all
// (iPad, iPod) produce nothing. No call here can fail.

#import <UIKit/UIKit.h>
#import <AudioToolbox/AudioToolbox.h>
#import <CoreHaptics/CoreHaptics.h>

static const CFTimeInterval FALLBACK_MIN_INTERVAL = 0.4;

static BOOL s_hapticEnabled = YES;
static int s_supportsHaptics = -1; // -1 unknown, 0 no, 1 yes
static CFTimeInterval s_lastFallbackTime = 0;
static UIImpactFeedbackGenerator *s_impact[5];
static UINotificationFeedbackGenerator *s_notification;
static UISelectionFeedbackGenerator *s_selection;

static BOOL supportsHaptics() {
    if (s_supportsHaptics < 0) {
        s_supportsHaptics = [CHHapticEngine capabilitiesForHardware].supportsHaptics ? 1 : 0;
    }
    return s_supportsHaptics == 1;
}

static UIImpactFeedbackGenerator *impactGenerator(int type) {
    if (s_impact[type] == nil) {
        UIImpactFeedbackStyle style = UIImpactFeedbackStyleLight;
        switch (type) {
            case 1: style = UIImpactFeedbackStyleMedium; break;
            case 2: style = UIImpactFeedbackStyleHeavy; break;
            case 3: style = UIImpactFeedbackStyleSoft; break;
            case 4: style = UIImpactFeedbackStyleRigid; break;
            default: break;
        }
        s_impact[type] = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [s_impact[type] prepare];
    }
    return s_impact[type];
}

static void playFallback() {
    CFTimeInterval now = CACurrentMediaTime();
    if (now - s_lastFallbackTime < FALLBACK_MIN_INTERVAL) return;
    s_lastFallbackTime = now;
    AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
}

static void playFeedback(int type) {
    if (!s_hapticEnabled) return;
    if (type < 0 || type > 8) return;

    if (!supportsHaptics()) {
        playFallback();
        return;
    }

    if (type <= 4) {
        UIImpactFeedbackGenerator *generator = impactGenerator(type);
        [generator impactOccurred];
        [generator prepare];
    } else if (type <= 7) {
        if (s_notification == nil) s_notification = [[UINotificationFeedbackGenerator alloc] init];
        UINotificationFeedbackType notification = type == 5 ? UINotificationFeedbackTypeSuccess
            : type == 6 ? UINotificationFeedbackTypeWarning
            : UINotificationFeedbackTypeError;
        [s_notification notificationOccurred:notification];
        [s_notification prepare];
    } else {
        if (s_selection == nil) s_selection = [[UISelectionFeedbackGenerator alloc] init];
        [s_selection selectionChanged];
        [s_selection prepare];
    }
}

extern "C" {
    void _unityHapticFeedback(int type) {
        // Unity calls in on its main thread; UIKit feedback generators must be used there too.
        if ([NSThread isMainThread]) {
            playFeedback(type);
        } else {
            dispatch_async(dispatch_get_main_queue(), ^{ playFeedback(type); });
        }
    }

    bool _unityHapticIsSupport() {
        // iPhones vibrate either way (haptics or the basic fallback); iPads/iPods have no motor.
        return supportsHaptics() || [UIDevice currentDevice].userInterfaceIdiom == UIUserInterfaceIdiomPhone;
    }

    void _unityHapticEnable() {
        s_hapticEnabled = YES;
    }

    bool _unityHapticDisable() {
        s_hapticEnabled = NO;
        return true;
    }
}
