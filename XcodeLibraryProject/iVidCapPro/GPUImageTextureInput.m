#import "GPUImageTextureInput.h"

extern EAGLContext* ivcp_UnityGetContext();

@implementation GPUImageTextureInput

#pragma mark -
#pragma mark Initialization and teardown

- (id)initWithTexture:(GLuint)newInputTexture size:(CGSize)newTextureSize;
{
    //NSLog(@"GPUImageTextureInput: initWithTexture called...\n");
    
    if (!(self = [super init]))
    {
        return nil;
    }
    
    runSynchronouslyOnVideoProcessingQueue(^{
        [GPUImageOpenGLESContext useImageProcessingContext];

        [self deleteOutputTexture];
    });
    
    outputTexture = newInputTexture;
    textureSize = newTextureSize;
    
    return self;
}

#pragma mark -
#pragma mark Image rendering

- (void)processTextureWithFrameTime:(CMTime)frameTime;
{
    runSynchronouslyOnVideoProcessingQueue(^{
        //   dispatch_async([GPUImageOpenGLESContext sharedOpenGLESQueue], ^{
        
        // eOe start
        // 16-Feb-2014 It appears both glFlush and glFinish are unneeded here and
        //             when present cause a substantial performance hit.  This was
        //             reported by Brian Chasalow.
        //glFlush();
        //glFinish();
        EAGLContext* unity_context = ivcp_UnityGetContext();
        
        for (id<GPUImageInput> currentTarget in targets)
        {
            NSInteger indexOfObject = [targets indexOfObject:currentTarget];
            NSInteger targetTextureIndex = [[targetTextureIndices objectAtIndex:indexOfObject] integerValue];
            
            [currentTarget setInputSize:textureSize atIndex:targetTextureIndex];
            [currentTarget newFrameReadyAtTime:frameTime atIndex:targetTextureIndex];
        }
        
        //glFlush();
        //glFinish();
        [EAGLContext setCurrentContext: unity_context];
        // eOe end
    });
}

@end
