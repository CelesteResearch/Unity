cmake_minimum_required(VERSION 3.24)
project(CelesteGraphicsUnity LANGUAGES C CXX)
if(APPLE)
    set(CMAKE_BUILD_WITH_INSTALL_RPATH ON)
    set(CMAKE_INSTALL_RPATH "@loader_path")
    set(CMAKE_INSTALL_NAME_DIR "@rpath")
    set(CMAKE_BUILD_WITH_INSTALL_NAME_DIR ON)
elseif(UNIX)
    set(CMAKE_BUILD_WITH_INSTALL_RPATH ON)
    set(CMAKE_INSTALL_RPATH "$ORIGIN")
endif()
set(UNITY_PLUGIN_API
    ""
    CACHE PATH "Unity Editor PluginAPI directory")
if(NOT EXISTS "${UNITY_PLUGIN_API}/IUnityInterface.h")
    message(FATAL_ERROR "Set UNITY_PLUGIN_API to the installed Unity Editor Data/PluginAPI directory")
endif()
set(CELESTE_SHARED
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../Shared"
    CACHE PATH "Shared checkout")
if(WIN32)
    set(CELESTE_RUNTIME_ID win-x64)
elseif(APPLE)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
        set(CELESTE_RUNTIME_ID osx-arm64)
    else()
        set(CELESTE_RUNTIME_ID osx-x64)
    endif()
else()
    set(CELESTE_RUNTIME_ID linux-x64)
endif()
set(CELESTE_NATIVE_RUNTIME
    "${CELESTE_SHARED}/artifacts/runtime/${CELESTE_RUNTIME_ID}"
    CACHE PATH "Built shared native libraries")
if(WIN32)
    add_library(CelesteGraphicsD3D11 SHARED IMPORTED)
    set_target_properties(
        CelesteGraphicsD3D11
        PROPERTIES
            IMPORTED_LOCATION "${CELESTE_NATIVE_RUNTIME}/CelesteGraphicsD3D11.dll"
            IMPORTED_IMPLIB "${CELESTE_NATIVE_RUNTIME}/CelesteGraphicsD3D11.lib"
            INTERFACE_INCLUDE_DIRECTORIES
            "${CELESTE_SHARED}/native/Graphics/Direct3D11;${CELESTE_SHARED}/vendor/mojoshader-ad5dff84830c2863c841f4b1f4e3df78c705b383"
    )
    # Core is SHARED, never compiled into this adapter. Both managed interop and this
    # adapter resolve the same CelesteGraphicsD3D11.dll and the same device instance.
    add_library(CelesteGraphicsUnityD3D11 SHARED Direct3D11/UnityDirect3D11Plugin.cpp
                                                 Direct3D11/UnityHostCameraCopy.cpp Direct3D11/UnityTextureViewLoan.cpp)
    target_compile_features(CelesteGraphicsUnityD3D11 PRIVATE cxx_std_17)
    target_include_directories(CelesteGraphicsUnityD3D11 PRIVATE "${UNITY_PLUGIN_API}")
    target_compile_definitions(CelesteGraphicsUnityD3D11 PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX MOJOSHADER_EFFECT_SUPPORT)
    target_compile_options(CelesteGraphicsUnityD3D11 PRIVATE /utf-8)
    target_link_libraries(CelesteGraphicsUnityD3D11 PRIVATE CelesteGraphicsD3D11 d3d11 dxguid)

endif()

add_library(CelesteGraphicsVulkan SHARED IMPORTED)
set_target_properties(
    CelesteGraphicsVulkan
    PROPERTIES
        IMPORTED_LOCATION
        "${CELESTE_NATIVE_RUNTIME}/${CMAKE_SHARED_LIBRARY_PREFIX}CelesteGraphicsVulkan${CMAKE_SHARED_LIBRARY_SUFFIX}")
if(WIN32)
    set_target_properties(CelesteGraphicsVulkan PROPERTIES IMPORTED_IMPLIB
                                                           "${CELESTE_NATIVE_RUNTIME}/CelesteGraphicsVulkan.lib")
endif()
if(NOT APPLE)
    add_library(CelesteGraphicsUnityVulkan SHARED Vulkan/UnityVulkanBridge.cpp Vulkan/UnityVulkanInitialization.cpp
                                                  Vulkan/UnityVulkanOperations.cpp)
    set_target_properties(CelesteGraphicsUnityVulkan PROPERTIES OUTPUT_NAME GfxPluginCelesteGraphicsUnityVulkan)
    target_compile_features(CelesteGraphicsUnityVulkan PRIVATE cxx_std_17)
    target_include_directories(
        CelesteGraphicsUnityVulkan PRIVATE "${UNITY_PLUGIN_API}" "${CELESTE_SHARED}/native/Graphics/Include"
                                           "${CELESTE_SHARED}/vendor/Vulkan-Headers-1.3.239")
    target_compile_definitions(CelesteGraphicsUnityVulkan PRIVATE VK_NO_PROTOTYPES WIN32_LEAN_AND_MEAN NOMINMAX)
    if(MSVC)
        target_compile_options(CelesteGraphicsUnityVulkan PRIVATE /utf-8 /W4)
    else()
        target_compile_options(CelesteGraphicsUnityVulkan PRIVATE -Wall -Wextra)
    endif()
    target_link_libraries(CelesteGraphicsUnityVulkan PRIVATE CelesteGraphicsVulkan)
endif()

if(WIN32)
    add_library(CelesteGraphicsDirect3D12 SHARED IMPORTED)
    set_target_properties(
        CelesteGraphicsDirect3D12 PROPERTIES IMPORTED_LOCATION "${CELESTE_NATIVE_RUNTIME}/CelesteGraphicsDirect3D12.dll"
                                             IMPORTED_IMPLIB "${CELESTE_NATIVE_RUNTIME}/CelesteGraphicsDirect3D12.lib")
    add_library(
        CelesteGraphicsUnityDirect3D12 SHARED Direct3D12/UnityDirect3D12Adapter.cpp
                                              Direct3D12/UnityDirect3D12Bridge.cpp Direct3D12/UnityDirect3D12Calls.cpp)
    set_target_properties(CelesteGraphicsUnityDirect3D12 PROPERTIES OUTPUT_NAME GfxPluginCelesteGraphicsUnityDirect3D12)
    target_compile_features(CelesteGraphicsUnityDirect3D12 PRIVATE cxx_std_17)
    target_include_directories(CelesteGraphicsUnityDirect3D12 PRIVATE "${UNITY_PLUGIN_API}"
                                                                      "${CELESTE_SHARED}/native/Graphics/Include")
    target_compile_definitions(CelesteGraphicsUnityDirect3D12 PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
    target_compile_options(CelesteGraphicsUnityDirect3D12 PRIVATE /utf-8)
    target_link_libraries(CelesteGraphicsUnityDirect3D12 PRIVATE CelesteGraphicsDirect3D12 d3d12 dxgi)
endif()

if(APPLE)
    enable_language(OBJCXX)
    add_library(CelesteUnityPlatform SHARED Platform/UnityPlatform.Mac.mm)
    target_link_libraries(CelesteUnityPlatform PRIVATE "-framework Cocoa")
elseif(UNIX)
    find_package(X11 REQUIRED)
    add_library(CelesteUnityPlatform SHARED Platform/UnityPlatform.Linux.cpp)
    target_link_libraries(CelesteUnityPlatform PRIVATE X11::X11)
endif()
if(TARGET CelesteUnityPlatform)
    target_compile_features(CelesteUnityPlatform PRIVATE cxx_std_17)

endif()

if(APPLE)
    add_library(CelesteGraphicsUnityMetal SHARED Metal/UnityMetalBridge.mm)
    set_target_properties(CelesteGraphicsUnityMetal PROPERTIES OUTPUT_NAME GfxPluginCelesteGraphicsUnityMetal)
    target_compile_features(CelesteGraphicsUnityMetal PRIVATE cxx_std_17)
    target_include_directories(
        CelesteGraphicsUnityMetal PRIVATE "${UNITY_PLUGIN_API}" "${CELESTE_SHARED}/native/Graphics/Include"
                                          "${CELESTE_SHARED}/vendor/Vulkan-Headers-1.3.239")
    target_compile_definitions(CelesteGraphicsUnityMetal PRIVATE VK_NO_PROTOTYPES)
    target_link_libraries(CelesteGraphicsUnityMetal PRIVATE CelesteGraphicsVulkan "-framework Metal"
                                                            "-framework Foundation")
endif()
