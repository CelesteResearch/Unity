#pragma once
#include <cstdint>
#define CELESTE_PLATFORM_EXPORT extern "C" __attribute__((visibility("default")))
CELESTE_PLATFORM_EXPORT void* CelesteUnityGetWindow();
CELESTE_PLATFORM_EXPORT int CelesteUnityWindowBounds(void*, int*, int*, int*, int*);
CELESTE_PLATFORM_EXPORT int CelesteUnityWindowStyle(void*, int, int);
CELESTE_PLATFORM_EXPORT int CelesteUnityWindowTitle(void*, const char*);
CELESTE_PLATFORM_EXPORT int CelesteUnityRaiseWindow(void*);
CELESTE_PLATFORM_EXPORT int CelesteUnityClientToScreen(void*, int*, int*);
