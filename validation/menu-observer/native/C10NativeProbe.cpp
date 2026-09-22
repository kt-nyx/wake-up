// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Bounded device-only experiment. No context, rendering, Unity ABI or hooks.
#include <d3d11.h>
#include <algorithm>
#include <cstdint>
#include <cstring>

struct Result {
    uint32_t deviceFlags, featureLevel, width, height, mips, format;
    uint32_t mainThread, workerThread, created, descriptorMatched;
};
static uint32_t Word(const unsigned char* bytes, size_t at) {
    uint32_t value;
    std::memcpy(&value, bytes + at, sizeof(value));
    return value;
}
extern "C" __declspec(dllexport) HRESULT __cdecl Bootstrap(
    ID3D11Resource* unityResource, ID3D11Device** retainedDevice, Result* result) {
    if (!unityResource || !retainedDevice || !result) return E_INVALIDARG;
    *result = {};
    *retainedDevice = nullptr;
    unityResource->GetDevice(retainedDevice); // Documented GetNativeTexturePtr D3D11 resource.
    if (!*retainedDevice) return E_FAIL;
    result->deviceFlags = (*retainedDevice)->GetCreationFlags();
    result->featureLevel = (*retainedDevice)->GetFeatureLevel();
    result->mainThread = GetCurrentThreadId();
    return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __cdecl CreateAuthoredDds(
    ID3D11Device* device, const unsigned char* dds, uint32_t length, Result* result) {
    if (!device || !dds || !result || length < 128 || length > 1024 * 1024) return E_INVALIDARG;
    if (device->GetCreationFlags() & D3D11_CREATE_DEVICE_SINGLETHREADED) return E_ACCESSDENIED;
    result->workerThread = GetCurrentThreadId();
    if (result->workerThread == result->mainThread) return E_UNEXPECTED;
    if (Word(dds, 0) != 0x20534444 || Word(dds, 4) != 124 || Word(dds, 76) != 32
        || Word(dds, 112) != 0 || Word(dds, 24) > 1) return E_INVALIDARG;
    const auto width = Word(dds, 16), height = Word(dds, 12), mips = Word(dds, 28);
    const auto fourCC = Word(dds, 84);
    const bool bc1 = fourCC == 0x31545844, bc3 = fourCC == 0x35545844;
    if ((!bc1 && !bc3) || !(Word(dds, 80) & 4) || !width || !height
        || width > 16384 || height > 16384 || !mips || mips > 15) return E_INVALIDARG;
    uint32_t completeMips = 1;
    for (uint32_t dim = (std::max)(width, height); dim > 1; dim >>= 1) ++completeMips;
    if (mips != completeMips) return E_INVALIDARG;
    D3D11_SUBRESOURCE_DATA data[15]{};
    size_t offset = 128;
    for (uint32_t mip = 0; mip < mips; ++mip) {
        const auto w = (std::max)(1u, width >> mip), h = (std::max)(1u, height >> mip);
        const uint32_t rowBytes = ((w + 3) / 4) * (bc1 ? 8 : 16);
        const uint32_t bytes = rowBytes * ((h + 3) / 4);
        if (offset + bytes > length) return E_INVALIDARG;
        data[mip] = {dds + offset, rowBytes, bytes};
        offset += bytes;
    }
    if (offset != length) return E_INVALIDARG;
    D3D11_TEXTURE2D_DESC desc{};
    desc.Width = width; desc.Height = height; desc.MipLevels = mips; desc.ArraySize = 1;
    desc.Format = bc1 ? DXGI_FORMAT_BC1_UNORM : DXGI_FORMAT_BC3_UNORM;
    desc.SampleDesc.Count = 1; desc.Usage = D3D11_USAGE_IMMUTABLE;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    ID3D11Texture2D* texture = nullptr;
    const HRESULT hr = device->CreateTexture2D(&desc, data, &texture);
    if (FAILED(hr)) return hr;
    result->created = 1;
    D3D11_TEXTURE2D_DESC actual{};
    texture->GetDesc(&actual);
    result->width = actual.Width; result->height = actual.Height;
    result->mips = actual.MipLevels; result->format = actual.Format;
    result->descriptorMatched = std::memcmp(&actual, &desc, sizeof(desc)) == 0;
    texture->Release(); // No Unity wrapper ever refers to this experiment's allocation.
    return result->descriptorMatched ? S_OK : E_FAIL;
}
extern "C" __declspec(dllexport) void __cdecl ReleaseDevice(ID3D11Device* device) {
    if (device) device->Release();
}
