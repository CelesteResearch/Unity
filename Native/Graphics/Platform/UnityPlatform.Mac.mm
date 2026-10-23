#include "UnityPlatform.h"
#import <Cocoa/Cocoa.h>

namespace
{
    NSWindow* Window(void* handle)
    {
        NSWindow* window = (__bridge NSWindow*)handle;
        return [NSApp.windows containsObject:window] ? window : nil;
    }
}

void* CelesteUnityGetWindow()
{
    NSWindow* window = NSApp.mainWindow;
    if (!window)
        window = NSApp.keyWindow;
    return (__bridge void*)window;
}

int CelesteUnityWindowBounds(void* handle, int* x, int* y, int* width, int* height)
{
    NSWindow* window = Window(handle);
    if (!window)
        return 0;
    NSRect frame = [window contentRectForFrameRect:window.frame];
    NSRect screen = NSScreen.screens.firstObject.frame;
    *x = static_cast<int>(frame.origin.x);
    *y = static_cast<int>(NSMaxY(screen) - NSMaxY(frame));
    *width = static_cast<int>(frame.size.width);
    *height = static_cast<int>(frame.size.height);
    return 1;
}

int CelesteUnityWindowStyle(void* handle, int borderless, int enabled)
{
    NSWindow* window = Window(handle);
    if (!window)
        return -1;
    const NSWindowStyleMask bit = borderless ? NSWindowStyleMaskTitled : NSWindowStyleMaskResizable;
    if (enabled < 0)
        return borderless ? !(window.styleMask & bit) : !!(window.styleMask & bit);
    const bool set = borderless ? !enabled : enabled;
    window.styleMask = set ? window.styleMask | bit : window.styleMask & ~bit;
    return 1;
}

int CelesteUnityWindowTitle(void* handle, const char* title)
{
    NSWindow* window = Window(handle);
    NSString* value = title ? [NSString stringWithUTF8String:title] : nil;
    if (!window || !value)
        return 0;
    window.title = value;
    return 1;
}

int CelesteUnityRaiseWindow(void* handle)
{
    NSWindow* window = Window(handle);
    if (!window)
        return 0;
    [window makeKeyAndOrderFront:nil];
    [NSApp activateIgnoringOtherApps:YES];
    return 1;
}

int CelesteUnityClientToScreen(void* handle, int* x, int* y)
{
    int left = 0, top = 0, width = 0, height = 0;
    if (!CelesteUnityWindowBounds(handle, &left, &top, &width, &height))
        return 0;
    *x += left;
    *y += top;
    return 1;
}
