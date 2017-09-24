// iVidCapPro Copyright (c) 2012-2014 James Allen and eccentric Orbits entertainment(eOe).
/*
 Permission is hereby granted, free of charge, to any person or organization obtaining a copy of
 the software and accompanying documentation covered by this license (the "Software") to use
 and prepare derivative works of the Software, for commercial or other purposes, excepting that
 the Software may not be repackaged for sale as a Unity asset.
 
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE,
 TITLE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING
 THE SOFTWARE BE LIABLE FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
 ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 IN THE SOFTWARE.
 
 
 The copyright notices in the Software and this entire statement, including the above license grant,
 this restriction and the following disclaimer, must be included in all copies of the Software,
 in whole or in part.
 */

//
//  -- iVidCapUtil.m --
//

/* ---------------------------------------------------------------------
 Change History:
 
 - 22 Oct 14 - Created. 
 --------------------------------------------------------------------- */

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

#import "iVidUtil.h"

// Use this to send messages to the iVidCapPro plugin on the Unity-side.
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

@implementation ivcp_Utility

/*
 --------------------------------------------------------------------------------
 -- singleton --
 --------------------------------------------------------------------------------
 */
+(ivcp_Utility *)singleton {
    static dispatch_once_t pred;
    static ivcp_Utility *shared = nil;
    dispatch_once(&pred, ^{
        shared = [[ivcp_Utility alloc] init];
    });
    return shared;
}


/* 
 --------------------------------------------------------------------------------
 -- copyVideoToPhotoAlbum --
 -------------------------------------------------------------------------------- 
*/
- (int) copyVideoToPhotoAlbum: (NSString *) videoPath CompletionTarget: (id) completionTarget CompletionSelector: (SEL) completionSelector {
    
    int result = 0;
    
    if ([self showDebug])
        NSLog(@"iVidUtil-copyVideoToPhotoAlbum: Saving video file=%@ to photo album.", videoPath);
    
    if (UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(videoPath)) {
        if ([self showDebug])
            NSLog(@"iVidUtil-copyVideoToPhotoAlbum: Video IS compatible. Adding it to photo album.");
        
        UISaveVideoAtPathToSavedPhotosAlbum(videoPath, completionTarget, completionSelector, nil);
    } else {
        if ([self showDebug])
            NSLog(@"iVidUtil-copyVideoToPhotoAlbum: Video IS NOT compatible. Could not be added to the photo album.");
        
        // Video not compatible with photo album.
        result = -1;
    }
    return result;
}

/*
 --------------------------------------------------------------------------------
 -- sendMessageToUnityObject --
 --------------------------------------------------------------------------------
 */
-(void) sendMessageToUnityObject: (const char*) objectName Method: (const char*) methodName Message: (NSString*) message {
    if ([self showDebug])
        NSLog(@"iVidUtil-sendMessageToUnityObject: objectName=%s methodName=%s message=%@", objectName, methodName, message);
    
    //NSString* message = [NSString stringWithFormat:@"error: %@ reason: %@", error, reason];
    
    UnitySendMessage(objectName, methodName, [message UTF8String]);
    
}

@end
