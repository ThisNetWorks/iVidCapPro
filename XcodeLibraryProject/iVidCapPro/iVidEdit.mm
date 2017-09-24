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
//  -- iVidCapEdit.mm --
//

/* ---------------------------------------------------------------------
 Change History:
 
 - 26 Oct 14 - Created.
 --------------------------------------------------------------------- */

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

#import "iVidEdit.h"
#import "iVidUtil.h"

@implementation ivcp_VideoEditor

/*
 --------------------------------------------------------------------------------
 -- singleton --
 --------------------------------------------------------------------------------
 */
+(ivcp_VideoEditor *)singleton {
    static dispatch_once_t pred;
    static ivcp_VideoEditor *shared = nil;
    dispatch_once(&pred, ^{
        shared = [[ivcp_VideoEditor alloc] init];
        
        // Allocate a C string for the name of the Unity message object.
        // No notification messages will be sent back to Unity unless this
        // gets filled-in with a valid string.
        shared.iVidEditUnityObject = (char*)malloc(256);
        strcpy(shared->iVidEditUnityObject, "");
        
        // Get reference to the utility object.
        shared->util = [ivcp_Utility singleton];
    });
    return shared;
}

/* 
 --------------------------------------------------------------------------------
 -- copyVideoToPhotoAlbum --
 --------------------------------------------------------------------------------
 */
- (int) copyVideoFileToPhotoAlbum: (NSString *) videoPath {
    
    //NSLog(@"iVidEdit-CopyVideoToPhotoAlbum: iVidEditUnityObject=%s\n", iVidEditUnityObject);
    
    int result = [util copyVideoToPhotoAlbum: videoPath CompletionTarget:self CompletionSelector:@selector(copyVideoFileToPhotoAlbumComplete: didFinishSavingWithError: contextInfo:)];
    
    return result;
}

/*
 --------------------------------------------------------------------------------
 -- copyVideoFileToPhotoAlbumComplete --
 --------------------------------------------------------------------------------
 */
- (void) copyVideoFileToPhotoAlbumComplete: (NSString *) videoPath
              didFinishSavingWithError: (NSError *) error
                           contextInfo: (void *) contextInfo {
    
    // Tell the Unity side that we've completed moving the video to the photo album.
    if (strcmp(iVidEditUnityObject, "") != 0)
    {
        if ([error code] == 0) {
            // Successful - Send move succeeded message to completion handler.
            NSString* message = [NSString stringWithFormat:@"%s:%s:%s", OP_COPY_FILE, STATUS_OK,  REASON_SUCCESS];
            
            if ([self getShowDebug])
                NSLog(@"iVidEdit-CopyVideoToPhotoAlbumComplete: videoPath=%@ error=%@ iVidEditUnityObject=%s\n", videoPath, error, iVidEditUnityObject);
            
            [[ivcp_Utility singleton] sendMessageToUnityObject:iVidEditUnityObject Method:"NotificationHandler" Message:message];
        }
        else {
            // Failed - Send move failed message.
            NSString* message = [NSString stringWithFormat:@"%s:%s:%s", OP_COPY_FILE, STATUS_FAILED,  REASON_FAILED_COPY];
            
            if ([self getShowDebug])
                NSLog(@"iVidEdit-CopyVideoToPhotoAlbumComplete: Failed! error code=%ld description=%@\n", (long)[error code], [error localizedDescription]);
            
            [[ivcp_Utility singleton] sendMessageToUnityObject:iVidEditUnityObject Method:"NotificationHandler" Message:message];
        }
    }
    
}

/*
 --------------------------------------------------------------------------------
 -- Debug --
 --------------------------------------------------------------------------------
 */
-(void) setShowDebug:(bool)value {
    showDebug = value;
    
    // Set the util debug value.
    if (util != nil) {
        [util setShowDebug:value];
    }
}

-(bool) getShowDebug {
    return showDebug;
}

/*
 --------------------------------------------------------------------------------
 -- get/set iVidEditUnityObject --
 --------------------------------------------------------------------------------
 */
-(char*) getIVidEditUnityObject {
    return iVidEditUnityObject;
}
-(void)  setIVidEditUnityObject: (char*) value {
    strcpy(iVidEditUnityObject, value);
}


@end

/* --------------------------------------------------------------------------------
 -- Plugin External Interface --
 
 -------------------------------------------------------------------------------- */

extern "C" {
    
    static ivcp_VideoEditor *ve;
    
    // Init the plugin.
    void ivcp_VE_Init(char* commObjectName) {
        
        // Create the video editor object.
        ve = [ivcp_VideoEditor singleton];
        
        
        // Set the Unity-side message object name.
        [ve setIVidEditUnityObject:commObjectName];
        
    }
    
    // Set the debug flag.
    void ivcp_VE_ShowDebug(bool value) {
        [ve setShowDebug:value];
    }
    
    //  Display the specified message using NSLog.
    void ivcp_VE_Log(const char* message)
    {
        NSString *nString = [NSString stringWithCString:message encoding:NSUTF8StringEncoding];
        
        NSLog(@"%@", nString);
    }


    // Copy the specified video file to the device Photo Album.
    int ivcp_VE_CopyVideoFileToPhotoAlbum(char* videoFileName)
    {
        if ([ve getShowDebug]) {
            NSLog(@"ivcp_VE_CopyVideoFileToPhotoAlbum called: videoFileNamename=%s iVidEditUnityObject=%s", videoFileName, [ve getIVidEditUnityObject]);
        }
        
        NSString* fileName = [NSString stringWithCString:videoFileName encoding:NSUTF8StringEncoding];
        
        int result = [ve copyVideoFileToPhotoAlbum:fileName];
        
        return result;
    }
    
}

