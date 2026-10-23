#include "UnityVulkanInitialization.h"
#include <cstring>
#include <mutex>
#include <unordered_map>

namespace
{
    PFN_vkGetInstanceProcAddr originalGetProc = nullptr;
    std::mutex versionsGate;
    std::unordered_map<VkInstance, uint32_t> versions;
    std::unordered_map<VkDevice, VkPhysicalDeviceFeatures> enabledFeatures;
    PFN_vkCreateDevice originalCreateDevice = nullptr;
    PFN_vkGetDeviceProcAddr originalGetDeviceProc = nullptr;
    VkResult VKAPI_PTR CreateInstance(const VkInstanceCreateInfo* info, const VkAllocationCallbacks* allocator,
                                      VkInstance* instance)
    {
        const auto create = reinterpret_cast<PFN_vkCreateInstance>(originalGetProc(VK_NULL_HANDLE, "vkCreateInstance"));
        const VkResult result = create(info, allocator, instance);
        if (result == VK_SUCCESS)
        {
            const uint32_t requested = info && info->pApplicationInfo && info->pApplicationInfo->apiVersion
                                           ? info->pApplicationInfo->apiVersion
                                           : VK_API_VERSION_1_0;
            std::lock_guard<std::mutex> lock(versionsGate);
            versions[*instance] = requested;
        }
        return result;
    }
    void VKAPI_PTR DestroyInstance(VkInstance instance, const VkAllocationCallbacks* allocator)
    {
        const auto destroy = reinterpret_cast<PFN_vkDestroyInstance>(originalGetProc(instance, "vkDestroyInstance"));
        {
            std::lock_guard<std::mutex> lock(versionsGate);
            versions.erase(instance);
        }
        destroy(instance, allocator);
    }
    VkResult VKAPI_PTR CreateDevice(VkPhysicalDevice physical, const VkDeviceCreateInfo* info,
                                    const VkAllocationCallbacks* allocator, VkDevice* device)
    {
        VkPhysicalDeviceFeatures features{};
        if (info->pEnabledFeatures)
            features = *info->pEnabledFeatures;
        for (auto next = static_cast<const VkBaseInStructure*>(info->pNext); next; next = next->pNext)
            if (next->sType == VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2)
                features = reinterpret_cast<const VkPhysicalDeviceFeatures2*>(next)->features;
        const VkResult result = originalCreateDevice(physical, info, allocator, device);
        if (result == VK_SUCCESS)
        {
            std::lock_guard<std::mutex> lock(versionsGate);
            enabledFeatures[*device] = features;
        }
        return result;
    }
    void VKAPI_PTR DestroyDevice(VkDevice device, const VkAllocationCallbacks* allocator)
    {
        const auto destroy = reinterpret_cast<PFN_vkDestroyDevice>(originalGetDeviceProc(device, "vkDestroyDevice"));
        {
            std::lock_guard<std::mutex> lock(versionsGate);
            enabledFeatures.erase(device);
        }
        destroy(device, allocator);
    }
    PFN_vkVoidFunction VKAPI_PTR GetDeviceProc(VkDevice device, const char* name)
    {
        const auto actual = originalGetDeviceProc(device, name);
        if (actual && std::strcmp(name, "vkGetDeviceProcAddr") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(GetDeviceProc);
        if (actual && std::strcmp(name, "vkDestroyDevice") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(DestroyDevice);
        return actual;
    }
    PFN_vkVoidFunction VKAPI_PTR GetProc(VkInstance instance, const char* name)
    {
        const auto actual = originalGetProc(instance, name);
        if (!actual)
            return nullptr;
        if (std::strcmp(name, "vkGetInstanceProcAddr") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(GetProc);
        if (std::strcmp(name, "vkCreateDevice") == 0)
        {
            originalCreateDevice = reinterpret_cast<PFN_vkCreateDevice>(actual);
            return reinterpret_cast<PFN_vkVoidFunction>(CreateDevice);
        }
        if (std::strcmp(name, "vkGetDeviceProcAddr") == 0)
        {
            originalGetDeviceProc = reinterpret_cast<PFN_vkGetDeviceProcAddr>(actual);
            return reinterpret_cast<PFN_vkVoidFunction>(GetDeviceProc);
        }
        if (std::strcmp(name, "vkCreateInstance") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(CreateInstance);
        if (std::strcmp(name, "vkDestroyInstance") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(DestroyInstance);
        return actual;
    }
}
PFN_vkGetInstanceProcAddr UNITY_INTERFACE_API
CelesteUnityVulkanInterceptInitialization(PFN_vkGetInstanceProcAddr original, void*)
{
    originalGetProc = original;
    return GetProc;
}
uint32_t CelesteUnityVulkanRequestedVersion(VkInstance instance)
{
    std::lock_guard<std::mutex> lock(versionsGate);
    const auto found = versions.find(instance);
    return found == versions.end() ? 0 : found->second;
}

bool CelesteUnityVulkanEnabledFeatures(VkDevice device, VkPhysicalDeviceFeatures* features)
{
    std::lock_guard<std::mutex> lock(versionsGate);
    const auto found = enabledFeatures.find(device);
    if (found == enabledFeatures.end())
        return false;
    *features = found->second;
    return true;
}
bool CelesteUnityVulkanInitializationWasInvoked()
{
    return originalGetProc != nullptr;
}
