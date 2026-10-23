#include <algorithm>
#include <mutex>
#include <vector>
#include "FvSDK.h"

namespace
{
    std::mutex g_mutex;
    FvSession g_session = nullptr;
    bool g_initialized = false;

    template <typename T>
    bool ReadLatest(FvMetricType type, T& value)
    {
        Samples samples;
        samples.type = type;
        auto status = FvSDK_ReadData(g_session, &samples, 1);
        if (status != FV_INSUFFICIENT_BUFFER && status != FV_MORE_DATA && status != FV_SUCCESS)
            return false;
        if (samples.mNumSamples == 0)
            return false;

        std::vector<T> data(samples.mNumSamples);
        samples.mData = data.data();
        status = FvSDK_ReadData(g_session, &samples, 1);
        if (status != FV_SUCCESS && status != FV_MORE_DATA)
            return false;

        const auto count = std::min(samples.mNumSamples, data.size());
        if (count == 0)
            return false;
        value = data[count - 1];
        return true;
    }

    void StopUnlocked()
    {
        if (g_session != nullptr)
        {
            FvSDK_StopSession(g_session);
            FvSDK_DestroySession(g_session);
            g_session = nullptr;
        }
        if (g_initialized)
        {
            FvSDK_Shutdown();
            g_initialized = false;
        }
    }
}

extern "C" __declspec(dllexport) int __cdecl FlexHubFrameView_Start()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_session != nullptr)
        return FV_SUCCESS;

    auto status = FvSDK_Initialize();
    if (status != FV_SUCCESS)
        return status;
    g_initialized = true;

    status = FvSDK_CreateSession(&g_session);
    if (status != FV_SUCCESS)
    {
        StopUnlocked();
        return status;
    }

    FvMetricType metrics[] = { eCPUPwr, eCPUPkgTemp };
    status = FvSDK_EnableMetrics(g_session, metrics, static_cast<unsigned int>(std::size(metrics)));
    if (status != FV_SUCCESS)
    {
        StopUnlocked();
        return status;
    }

    status = FvSDK_StartSession(g_session);
    if (status != FV_SUCCESS)
        StopUnlocked();
    return status;
}

extern "C" __declspec(dllexport) int __cdecl FlexHubFrameView_Capture(double* packagePowerWatts, double* packageTemperatureC)
{
    if (packagePowerWatts == nullptr || packageTemperatureC == nullptr)
        return FV_ERROR_INVALID_ARGS;

    std::lock_guard<std::mutex> lock(g_mutex);
    *packagePowerWatts = 0;
    *packageTemperatureC = 0;
    if (g_session == nullptr)
        return FV_INVALID_SESSION;

    auto status = FvSDK_SampleData(g_session, FVSDK_SAMPLEDATA_DONOTWAIT);
    if (status != FV_SUCCESS)
        return status;

    CPUPwr power;
    CPUPkgTemp temperature;
    const bool hasPower = ReadLatest(eCPUPwr, power);
    const bool hasTemperature = ReadLatest(eCPUPkgTemp, temperature);
    if (hasPower)
        *packagePowerWatts = power.mPkgPwr;
    if (hasTemperature)
        *packageTemperatureC = temperature.mCPUPkgTemp;
    return hasPower || hasTemperature ? FV_SUCCESS : FV_NO_DATA;
}

extern "C" __declspec(dllexport) void __cdecl FlexHubFrameView_Stop()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    StopUnlocked();
}
