#include "UnityPlatform.h"
#include <X11/Xatom.h>
#include <X11/Xlib.h>
#include <X11/Xutil.h>
#include <unistd.h>
#include <cstring>

namespace
{
    Display* DisplayConnection()
    {
        static Display* display = XOpenDisplay(nullptr);
        return display;
    }
    Window WindowId(void* window)
    {
        return static_cast<Window>(reinterpret_cast<uintptr_t>(window));
    }
    bool OwnedWindow(Display* display, Window window)
    {
        Atom type = None;
        int format = 0;
        unsigned long count = 0, remaining = 0;
        unsigned char* value = nullptr;
        const Atom pid = XInternAtom(display, "_NET_WM_PID", False);
        const int result = XGetWindowProperty(display, window, pid, 0, 1, False, XA_CARDINAL, &type, &format, &count,
                                              &remaining, &value);
        const bool owned = result == Success && value && format == 32 && count == 1 &&
                           *reinterpret_cast<unsigned long*>(value) == static_cast<unsigned long>(getpid());
        if (value)
            XFree(value);
        return owned;
    }
}

void* CelesteUnityGetWindow()
{
    Display* display = DisplayConnection();
    if (!display)
        return nullptr;
    Atom type = None;
    int format = 0;
    unsigned long count = 0, remaining = 0;
    unsigned char* value = nullptr;
    Window found = None;
    const Atom clients = XInternAtom(display, "_NET_CLIENT_LIST", False);
    if (XGetWindowProperty(display, DefaultRootWindow(display), clients, 0, 65536, False, XA_WINDOW, &type, &format,
                           &count, &remaining, &value) != Success ||
        !value)
        return nullptr;
    auto windows = reinterpret_cast<Window*>(value);
    for (unsigned long i = 0; i < count; ++i)
    {
        if (!OwnedWindow(display, windows[i]))
            continue;
        if (found != None)
        {
            XFree(value);
            return nullptr;
        }
        found = windows[i];
    }
    XFree(value);
    return reinterpret_cast<void*>(static_cast<uintptr_t>(found));
}

int CelesteUnityWindowBounds(void* handle, int* x, int* y, int* width, int* height)
{
    Display* display = DisplayConnection();
    if (!display || !handle)
        return 0;
    XWindowAttributes attributes{};
    if (!XGetWindowAttributes(display, WindowId(handle), &attributes))
        return 0;
    Window child = None;
    if (!XTranslateCoordinates(display, WindowId(handle), DefaultRootWindow(display), 0, 0, x, y, &child))
        return 0;
    *width = attributes.width;
    *height = attributes.height;
    return 1;
}

int CelesteUnityWindowStyle(void* handle, int borderless, int enabled)
{
    Display* display = DisplayConnection();
    if (!display || !handle)
        return -1;
    Window window = WindowId(handle);
    if (borderless)
    {
        struct Hints
        {
            unsigned long flags, functions, decorations;
            long inputMode;
            unsigned long status;
        };
        const Atom property = XInternAtom(display, "_MOTIF_WM_HINTS", False);
        Atom type = None;
        int format = 0;
        unsigned long count = 0, remaining = 0;
        unsigned char* data = nullptr;
        Hints hints{};
        XGetWindowProperty(display, window, property, 0, 5, False, property, &type, &format, &count, &remaining, &data);
        if (data && count >= 5 && format == 32)
            std::memcpy(&hints, data, sizeof(Hints));
        if (data)
            XFree(data);
        if (enabled < 0)
            return (hints.flags & 2) && hints.decorations == 0;
        hints.flags |= 2;
        hints.decorations = enabled ? 0 : 1;
        XChangeProperty(display, window, property, property, 32, PropModeReplace,
                        reinterpret_cast<unsigned char*>(&hints), 5);
    }
    else
    {
        XSizeHints hints{};
        long supplied = 0;
        XGetWMNormalHints(display, window, &hints, &supplied);
        const bool fixed = (hints.flags & PMinSize) && (hints.flags & PMaxSize) && hints.min_width == hints.max_width &&
                           hints.min_height == hints.max_height;
        if (enabled < 0)
            return !fixed;
        if (enabled)
            hints.flags &= ~(PMinSize | PMaxSize);
        else
        {
            XWindowAttributes attributes{};
            if (!XGetWindowAttributes(display, window, &attributes))
                return -1;
            hints.flags |= PMinSize | PMaxSize;
            hints.min_width = hints.max_width = attributes.width;
            hints.min_height = hints.max_height = attributes.height;
        }
        XSetWMNormalHints(display, window, &hints);
    }
    XFlush(display);
    return 1;
}

int CelesteUnityWindowTitle(void* handle, const char* title)
{
    Display* display = DisplayConnection();
    if (!display || !handle || !title)
        return 0;
    const Atom utf8 = XInternAtom(display, "UTF8_STRING", False);
    const Atom name = XInternAtom(display, "_NET_WM_NAME", False);
    XChangeProperty(display, WindowId(handle), name, utf8, 8, PropModeReplace,
                    reinterpret_cast<const unsigned char*>(title), static_cast<int>(std::strlen(title)));
    XStoreName(display, WindowId(handle), title);
    XFlush(display);
    return 1;
}

int CelesteUnityRaiseWindow(void* handle)
{
    Display* display = DisplayConnection();
    if (!display || !handle)
        return 0;
    XEvent event{};
    event.xclient.type = ClientMessage;
    event.xclient.window = WindowId(handle);
    event.xclient.message_type = XInternAtom(display, "_NET_ACTIVE_WINDOW", False);
    event.xclient.format = 32;
    event.xclient.data.l[0] = 1;
    event.xclient.data.l[1] = CurrentTime;
    const int result = XSendEvent(display, DefaultRootWindow(display), False,
                                  SubstructureRedirectMask | SubstructureNotifyMask, &event);
    XFlush(display);
    return result != 0;
}

int CelesteUnityClientToScreen(void* handle, int* x, int* y)
{
    Display* display = DisplayConnection();
    if (!display || !handle)
        return 0;
    Window child = None;
    return XTranslateCoordinates(display, WindowId(handle), DefaultRootWindow(display), *x, *y, x, y, &child) != 0;
}
