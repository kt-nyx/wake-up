// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Fixture-only selected-provider observation. Native results are never replaced.
#include <windows.h>
#include <mmsystem.h>
#include <mmreg.h>
#include <msacm.h>
#include <msacmdrv.h>
#include <bcrypt.h>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <vector>
#include <float.h>
#include <xmmintrin.h>
#include <intrin.h>
#include "WakeUpAcm.h"
#include "vendor/minhook/include/MinHook.h"

namespace {
constexpr size_t Capacity = 512, FormatCapacity = 256;
constexpr uint32_t EntryRva = 0x3a10;
using Driver = LRESULT(CALLBACK*)(DWORD_PTR, HDRVR, UINT, LPARAM, LPARAM);
Driver original = nullptr;
SRWLOCK gate = SRWLOCK_INIT;
CONDITION_VARIABLE changed = CONDITION_VARIABLE_INIT;
// Diagnostics 13=precise installation refusal, 14=detail (RVA, native status,
// Win32 error or observed size). Codes 100..112 provider, 200..212 CRT;
// 300..306 dependency binding, 400..403 provider/pins, 410..412 MinHook.
// 15=mapped thunk's five bytes (little endian), 16=decoded jump target,
// 17=declared CFG dispatcher target, 18=accepted equivalent loader fixups.
// 37..44=last loader target region base/allocation/state/type/protection,
// readable byte count and first two little-endian 64-bit words. Diagnostic only.
uint64_t stats[45]{};
uint64_t serial = 0;
UINT lastConfiguration = 0;
bool installAttempted = false;
HANDLE retainedFile = INVALID_HANDLE_VALUE;
HANDLE retainedMathFile = INVALID_HANDLE_VALUE;
HMODULE retainedModule = nullptr;
HMODULE retainedMathModule = nullptr;
void* mathTargets[2]{};
using GetFma = int(__cdecl*)();
GetFma getFma = nullptr;
using Pow = double(__cdecl*)(double, double);
using PowWrapper = double(__cdecl*)(Pow, double, double);
using ErrnoPointer = int*(__cdecl*)();
using DosErrnoPointer = unsigned long*(__cdecl*)();
Pow originalPow = nullptr, originalFma = nullptr, originalSse = nullptr;
PowWrapper historicalWrapper = nullptr;
ErrnoPointer nativeErrno = nullptr;
DosErrnoPointer nativeDosErrno = nullptr;
bool bothBranchesAvailable = false;
unsigned int supportedCsrMask = 0xffffU;
extern "C" void wakeup_fp_save(void* state);
extern "C" void wakeup_fp_restore(const void* state);
struct alignas(16) FpImage { unsigned char bytes[512]; };
struct Effects { FpImage fp; int error; unsigned long dos; DWORD lastError; };
struct ControlTest { bool active = false, aba = false, abaInPow = false; unsigned int fma = 0; Effects saved{}; uint64_t abaReport[8]{}; };
thread_local ControlTest controlTest;
thread_local uint64_t cacheDiagnostics[12]{};
// Explicit disposable-fixture control only. Each boundary restores its own
// caller value; the production cache export still performs its original save.
struct LastErrorTest { bool active = false, boundary = false; DWORD saved = 0, sentinel = 0; uint64_t receipt[9]{}; };
thread_local LastErrorTest lastErrorTest;
using SetFma = int(__cdecl*)(int);
SetFma TestSetter();
unsigned int RawFma();
struct PowCall { unsigned int branches = 0; uint32_t branch = 0; uint64_t x = 0, y = 0; };
struct MathScope {
    bool active = false, replay = false, inDriver = false, verifying = false, valid = true;
    HACMSTREAM stream = nullptr;
    uint64_t context[8]{}, output[8]{};
    uint64_t calls = 0, pairs = 0, doubles = 0, suffixes = 0;
    unsigned int conversions = 0, refusal = 0;
    PowCall* call = nullptr;
};
thread_local MathScope mathScope;

uint64_t Bits(double value) { uint64_t bits; std::memcpy(&bits, &value, sizeof(bits)); return bits; }
unsigned int Csr(const FpImage& image) { unsigned int value; std::memcpy(&value, image.bytes + 24, 4); return value; }
void Capture(Effects& state) {
    wakeup_fp_save(&state.fp);
    state.lastError = GetLastError(); state.error = *nativeErrno(); state.dos = *nativeDosErrno();
    SetLastError(state.lastError);
}
void Restore(const Effects& state) {
    *nativeErrno() = state.error; *nativeDosErrno() = state.dos;
    wakeup_fp_restore(&state.fp); SetLastError(state.lastError);
}
bool SafePowArguments(uint64_t x, uint64_t y) {
    if (y != 0x3ff5555555555555ULL || (x >> 63)) return false;
    const unsigned int exponent = static_cast<unsigned int>((x >> 52) & 0x7ff);
    if (exponent < 1030 || exponent > 1053) return false;
    const unsigned int fraction = 52 - (exponent - 1023);
    return (x & ((1ULL << fraction) - 1)) == 0; // positive int 128..INT_MAX
}
bool NoUnexpectedEffects(const Effects& before, const Effects& after) {
    const unsigned int first = Csr(before.fp), last = Csr(after.fp);
    // Provider and qualified CRT paths are SSE-only. x87 environment/registers
    // must remain exact, controls unchanged, and sticky flags only accumulate.
    return before.error == after.error && before.dos == after.dos && before.lastError == after.lastError &&
        (first & 0xffc0U) == (last & 0xffc0U) && (first & 0x3fU & ~last) == 0 &&
        std::memcmp(before.fp.bytes, after.fp.bytes, 24) == 0 &&
        std::memcmp(before.fp.bytes + 32, after.fp.bytes + 32, 128) == 0;
}

#pragma float_control(precise, on, push)
// The pinned provider uses sign-XOR, separate MULSD and CVTSD2SS. Intrinsics
// retain that instruction boundary; no fused multiply-add or C++ float cast.
uint32_t Suffix(uint64_t bits, uint32_t scaleBits, bool negative) {
    if (negative) bits ^= 0x8000000000000000ULL;
    const __m128d value = _mm_castsi128_pd(_mm_cvtsi64_si128(static_cast<__int64>(bits)));
    const __m128 scaleFloat = _mm_castsi128_ps(_mm_cvtsi32_si128(static_cast<int>(scaleBits)));
    const __m128d scale = _mm_cvtps_pd(scaleFloat);
    const __m128d multiplied = _mm_mul_sd(value, scale);
    const __m128 narrowed = _mm_cvtsd_ss(_mm_setzero_ps(), multiplied);
    return static_cast<uint32_t>(_mm_cvtsi128_si32(_mm_castps_si128(narrowed)));
}
#pragma float_control(pop)

bool EquivalentPow(double actual, double alternate, const Effects& first, const Effects& second) {
    if (first.error != second.error || first.dos != second.dos || first.lastError != second.lastError ||
        std::memcmp(first.fp.bytes, second.fp.bytes, 24) != 0 ||
        std::memcmp(first.fp.bytes + 32, second.fp.bytes + 32, 128) != 0) return false;
    const unsigned int firstCsr = Csr(first.fp), secondCsr = Csr(second.fp);
    if ((firstCsr & 0xffc0U) != (secondCsr & 0xffc0U)) return false;
    const uint64_t a = Bits(actual), b = Bits(alternate);
    if (a == b && firstCsr == secondCsr) { ++mathScope.doubles; return true; }
    // Both authored pow callsites consume the result only through this suffix,
    // with one of all 128 exact pinned scales and either sign. Each pair begins
    // with its own real post-pow flags, including the wrapper's native effects.
    for (unsigned int scale = 0; scale < 128; ++scale) {
        uint32_t scaleBits;
        std::memcpy(&scaleBits, reinterpret_cast<unsigned char*>(retainedModule) + 0x11620 + scale * 4, 4);
        for (unsigned int sign = 0; sign < 2; ++sign) {
            _mm_setcsr(firstCsr); const uint32_t firstBits = Suffix(a, scaleBits, sign != 0);
            const unsigned int firstFlags = _mm_getcsr();
            _mm_setcsr(secondCsr); const uint32_t secondBits = Suffix(b, scaleBits, sign != 0);
            if (firstBits != secondBits || firstFlags != _mm_getcsr()) return false;
        }
    }
    ++mathScope.suffixes; return true;
}

double __cdecl ObserveFma(double x, double y) {
    if (mathScope.inDriver && !mathScope.verifying && mathScope.call) {
        ++mathScope.call->branches; mathScope.call->branch = 0x5e990;
        if (mathScope.call->x != Bits(x) || mathScope.call->y != Bits(y)) mathScope.valid = false;
        if (controlTest.abaInPow) ++controlTest.abaReport[2];
    }
    return originalFma(x, y);
}
double __cdecl ObserveSse(double x, double y) {
    if (mathScope.inDriver && !mathScope.verifying && mathScope.call) {
        ++mathScope.call->branches; mathScope.call->branch = 0x9dd10;
        if (mathScope.call->x != Bits(x) || mathScope.call->y != Bits(y)) mathScope.valid = false;
        if (controlTest.abaInPow) ++controlTest.abaReport[3];
    }
    return originalSse(x, y);
}

double __cdecl ObservePow(double x, double y) {
    if (!mathScope.inDriver) return originalPow(x, y);
    const uintptr_t caller = reinterpret_cast<uintptr_t>(_ReturnAddress()) - reinterpret_cast<uintptr_t>(retainedModule);
    if (caller != 0xad28 && caller != 0xb2c9) { mathScope.valid = false; mathScope.refusal = 1; return originalPow(x, y); }
    ++mathScope.calls;
    if (mathScope.calls > 4096) { mathScope.valid = false; mathScope.refusal = 4; return originalPow(x, y); }
    if (mathScope.replay) {
        // A qualified record proves both implementations equivalent at every
        // consumed suffix. Use the baseline SSE branch, regardless of live FMA.
        return historicalWrapper(reinterpret_cast<Pow>(reinterpret_cast<unsigned char*>(retainedMathModule) + 0x9dd10), x, y);
    }
    Effects before{}, after{}; Capture(before);
    const bool aba = controlTest.active && controlTest.aba && !mathScope.verifying;
    const unsigned int entryRaw = aba ? RawFma() : 0;
    if (aba) {
        if (!controlTest.abaReport[1]) controlTest.abaReport[6] = entryRaw;
        const int mode = static_cast<int>(controlTest.abaReport[1] & 1ULL);
        ++controlTest.abaReport[1]; ++controlTest.abaReport[4];
        TestSetter()(mode); Restore(before);
        controlTest.abaInPow = true;
    }
    PowCall call{}; call.x = Bits(x); call.y = Bits(y);
    PowCall* previous = mathScope.call; mathScope.call = &call;
    double result = 0;
    __try { result = originalPow(x, y); }
    __finally {
        mathScope.call = previous;
        if (aba) {
            Effects completed{}; Capture(completed);
            TestSetter()(static_cast<int>(entryRaw & 1U)); ++controlTest.abaReport[4];
            controlTest.abaReport[7] = RawFma();
            if (controlTest.abaReport[7] != entryRaw) { ++controlTest.abaReport[5]; mathScope.valid = false; }
            controlTest.abaInPow = false;
            Restore(completed);
        }
    }
    Capture(after);
    if (!bothBranchesAvailable || !SafePowArguments(call.x, call.y) || call.branches != 1 ||
        (Csr(before.fp) & 0x1f80U) != 0x1f80U || !NoUnexpectedEffects(before, after)) {
        mathScope.valid = false; mathScope.refusal = 2; return result;
    }
    mathScope.verifying = true;
    __try {
        Restore(before);
        const uint32_t other = call.branch == 0x5e990 ? 0x9dd10U : 0x5e990U;
        const double alternate = historicalWrapper(reinterpret_cast<Pow>(reinterpret_cast<unsigned char*>(retainedMathModule) + other), x, y);
        Effects alternateAfter{}; Capture(alternateAfter);
        if (NoUnexpectedEffects(before, alternateAfter) && EquivalentPow(result, alternate, after, alternateAfter)) ++mathScope.pairs;
        else { mathScope.valid = false; mathScope.refusal = 3; }
    }
    __finally { mathScope.verifying = false; Restore(after); }
    return result;
}

bool ValidMathContext(const uint64_t* context, uint32_t count) {
    return context && count == 8 && context[0] == 1 && context[1] <= 0xffff && context[2] <= 0xffff &&
        !(context[1] & ~static_cast<uint64_t>(supportedCsrMask)) && !(context[2] & ~static_cast<uint64_t>(supportedCsrMask)) &&
        (context[1] & 0x1f80) == 0x1f80 && (context[1] & 0xffc0) == (context[2] & 0xffc0) &&
        (context[1] & 0x3f & ~context[2]) == 0 && context[3] == context[4] &&
        context[5] + context[6] == context[4] && context[3] <= 4096 && static_cast<uint32_t>(context[7]) == 0x9dd10;
}

LRESULT RunDriverCore(DWORD_PTR id, HDRVR handle, UINT message, LPARAM first, LPARAM second) {
    auto* instance = reinterpret_cast<PACMDRVSTREAMINSTANCE>(first);
    if (!mathScope.active || message != ACMDM_STREAM_CONVERT || !instance || instance->has != mathScope.stream)
        return original(id, handle, message, first, second);
    Effects caller{}, entered{}, after{}; Capture(caller);
    const bool replay = mathScope.replay;
    LRESULT result = MMSYSERR_ERROR;
    ++mathScope.conversions;
    __try {
        if (replay) {
            _mm_setcsr(static_cast<unsigned int>(mathScope.context[1]));
            SetLastError(static_cast<DWORD>(mathScope.context[7] >> 32));
        }
        Capture(entered);
        mathScope.output[1] = Csr(entered.fp);
        mathScope.output[7] = (static_cast<uint64_t>(entered.lastError) << 32) | 0x9dd10;
        mathScope.inDriver = true;
        result = original(id, handle, message, first, second);
        Capture(after); mathScope.output[2] = Csr(after.fp);
        if (result != MMSYSERR_NOERROR || !NoUnexpectedEffects(entered, after)) mathScope.valid = false;
    }
    __finally {
        mathScope.inDriver = false;
        if (replay) Restore(caller);
    }
    return result;
}
LRESULT RunDriver(DWORD_PTR id, HDRVR handle, UINT message, LPARAM first, LPARAM second) {
    if (!lastErrorTest.active || lastErrorTest.boundary || message != ACMDM_STREAM_CONVERT)
        return RunDriverCore(id, handle, message, first, second);
    const DWORD outer = GetLastError();
    const auto* instance = reinterpret_cast<PACMDRVSTREAMINSTANCE>(first);
    const bool replay = mathScope.active && mathScope.replay && instance && instance->has == mathScope.stream;
    ++lastErrorTest.receipt[replay ? 3 : 2];
    lastErrorTest.boundary = true;
    LRESULT result = MMSYSERR_ERROR;
    __try {
        SetLastError(lastErrorTest.sentinel);
        result = RunDriverCore(id, handle, message, first, second);
    }
    __finally {
        if (GetLastError() == lastErrorTest.sentinel) ++lastErrorTest.receipt[6];
        else ++lastErrorTest.receipt[7];
        lastErrorTest.boundary = false;
        SetLastError(outer);
    }
    return result;
}
struct MathOverride { uint32_t operand, original, alternate; };
// Exact ucrtbase DVRT symbol7 records: each list contains ONE alternate RVA.
constexpr MathOverride mathOverrides[] = {
    {0xf8021,0x174e0,0xa88d0}, {0xf8031,0x88070,0xa88e0}, {0xf8041,0x8d6d0,0xa88b0},
    {0xf8051,0x82da0,0xa88c0}, {0xf8061,0xd03d0,0xa8920}, {0xf8071,0xd0430,0xa8940},
    {0xf8081,0xa4960,0xa8930}, {0xf8091,0x555b0,0xa8950}, {0xf80a1,0xa3d10,0xa8970},
    {0xf80b1,0xd04e0,0xa8960}, {0xf80c1,0x93920,0xa88f0}, {0xf80d1,0xd1320,0xa8910},
    {0xf80e1,0xd1380,0xa8900}
};
unsigned char selectedMathJumps[13][5]{};
unsigned char selectedCfgJumps[2][5]{};
uint64_t overrideIdentity[16]{}; // version, count, 13 chosen RVAs, reserved zero
bool mathJumpsBound = false, cfgJumpsBound[2]{};
uintptr_t equivalentCfgTarget[2]{}, equivalentCfgSlot[2]{};
void* equivalentCfgAllocation[2]{};
uint64_t equivalentCfgDispatcher[2]{};

bool Refuse(uint64_t reason, uint64_t detail = 0) {
    AcquireSRWLockExclusive(&gate); stats[13] = reason; stats[14] = detail; ReleaseSRWLockExclusive(&gate);
    return false;
}

struct Format {
    uint32_t size = 0;
    unsigned char bytes[FormatCapacity]{};
};
struct Slot {
    HACMSTREAM stream = nullptr;
    PACMDRVSTREAMINSTANCE instance = nullptr;
    uint64_t token = 0, epoch = 0, resets = 0, converts = 0, floatingPoint = 0;
    DWORD flags = 0;
    bool filter = false, callback = false, witnessed = false;
    Format source, destination;
};
Slot slots[Capacity];
struct Scope {
    bool active = false;
    HACMSTREAM stream = nullptr;
    void* source = nullptr;
    void* destination = nullptr;
    uint32_t sourceLength = 0, destinationLength = 0;
    uint64_t token = 0, epoch = 0, floatingPoint = 0;
    uint64_t receipt[AcmReceiptCount]{};
};
thread_local Scope scope;

bool FloatingPoint(uint64_t& value) {
    unsigned int control = 0;
    // mask=0 observes the current control mode without changing it. MXCSR's
    // six accrued exception-status flags are excluded; rounding, masks, FTZ
    // and DAZ remain part of this caller-thread conversion input.
    if (_controlfp_s(&control, 0, 0) != 0) return false;
    if (!getFma) return false;
    const int fma = getFma();
    if (fma != 0 && fma != 1) return false;
    value = (static_cast<uint64_t>(control) << 32) | (_mm_getcsr() & 0xffc0U) | static_cast<unsigned int>(fma);
    return true;
}
bool MathCurrent() {
    if (!(retainedModule && retainedMathModule && mathTargets[0] && mathTargets[1] && mathJumpsBound && cfgJumpsBound[0] && cfgJumpsBound[1] &&
        *reinterpret_cast<void* const*>(reinterpret_cast<unsigned char*>(retainedModule) + 0x105d8) == mathTargets[0] &&
        *reinterpret_cast<void* const*>(reinterpret_cast<unsigned char*>(retainedModule) + 0x105e0) == mathTargets[1])) return false;
    if (std::memcmp(reinterpret_cast<unsigned char*>(retainedModule) + 0xf010, selectedCfgJumps[0], 5) != 0 ||
        std::memcmp(reinterpret_cast<unsigned char*>(retainedMathModule) + 0xf8010, selectedCfgJumps[1], 5) != 0) return false;
    for (size_t i = 0; i < 2; ++i) {
        if (equivalentCfgSlot[i] && *reinterpret_cast<const uint64_t*>(equivalentCfgSlot[i]) != equivalentCfgDispatcher[i]) return false;
        if (!equivalentCfgTarget[i]) continue;
        const auto* local = reinterpret_cast<const unsigned char*>(static_cast<uintptr_t>(equivalentCfgDispatcher[i]));
        if (local[0] != 0xff || local[1] != 0xe0) return false;
        MEMORY_BASIC_INFORMATION region{};
        const auto* target = reinterpret_cast<const unsigned char*>(equivalentCfgTarget[i]);
        if (!VirtualQuery(target, &region, sizeof(region)) || region.State != MEM_COMMIT ||
            region.Protect != PAGE_EXECUTE_READ || region.Type != MEM_IMAGE ||
            region.AllocationBase != equivalentCfgAllocation[i] || region.RegionSize < 3 ||
            equivalentCfgTarget[i] < reinterpret_cast<uintptr_t>(region.BaseAddress) ||
            equivalentCfgTarget[i] - reinterpret_cast<uintptr_t>(region.BaseAddress) > region.RegionSize - 3 ||
            target[0] != 0x48 || target[1] != 0xff || target[2] != 0xe0) return false;
    }
    for (size_t i = 0; i < 13; ++i)
        if (std::memcmp(reinterpret_cast<unsigned char*>(retainedMathModule) + mathOverrides[i].operand - 1,
            selectedMathJumps[i], 5) != 0) return false;
    return true;
}

Slot* Find(HACMSTREAM stream) {
    for (auto& slot : slots) if (slot.stream == stream && stream) return &slot;
    return nullptr;
}
bool CopyFormat(Format& output, const WAVEFORMATEX* format) {
    if (!format || format->cbSize > FormatCapacity - sizeof(WAVEFORMATEX)) return false;
    output.size = static_cast<uint32_t>(sizeof(WAVEFORMATEX) + format->cbSize);
    std::memcpy(output.bytes, format, output.size);
    return true;
}
uint64_t Digest(const Format& format) {
    uint64_t value = 14695981039346656037ULL;
    for (uint32_t i = 0; i < format.size; ++i) value = (value ^ format.bytes[i]) * 1099511628211ULL;
    return value;
}
bool Equal(const Format& format, const void* bytes, uint32_t length) {
    return bytes && length == format.size && length && std::memcmp(format.bytes, bytes, length) == 0;
}
bool Configuration(UINT message) {
    return message == DRV_CONFIGURE || message == DRV_INSTALL || message == DRV_REMOVE ||
        message == DRV_ENABLE || message == DRV_DISABLE ||
        (message >= ACMDM_USER && message < ACMDM_RESERVED_LOW);
}

LRESULT CALLBACK Observe(DWORD_PTR driverId, HDRVR driverHandle, UINT message, LPARAM first, LPARAM second) {
    AcquireSRWLockExclusive(&gate);
    ++stats[3];
    const bool observing = stats[1] != 0;
    const bool configuration = observing && Configuration(message);
    if (observing) ++stats[2];
    if (configuration) { ++stats[6]; ++stats[11]; lastConfiguration = message; }
    const uint64_t entryEpoch = stats[6];
    ReleaseSRWLockExclusive(&gate);
    Slot opening;
    auto* instance = reinterpret_cast<PACMDRVSTREAMINSTANCE>(first);
    const bool openInput = observing && message == ACMDM_STREAM_OPEN && instance &&
        instance->cbStruct >= sizeof(ACMDRVSTREAMINSTANCE) &&
        CopyFormat(opening.source, instance->pwfxSrc) && CopyFormat(opening.destination, instance->pwfxDst);
    if (openInput) {
        opening.flags = instance->fdwOpen; opening.filter = instance->pwfltr != nullptr;
        opening.callback = instance->dwCallback != 0;
    }
    uint64_t entryFloatingPoint = 0;
    const bool floatingPointObserved = observing && (message == ACMDM_STREAM_OPEN ||
        (scope.active && message == ACMDM_STREAM_CONVERT)) && FloatingPoint(entryFloatingPoint);
    if (openInput && floatingPointObserved) opening.floatingPoint = entryFloatingPoint;
    // No observer lock or managed work spans the real provider call.
    const LRESULT result = RunDriver(driverId, driverHandle, message, first, second);
    if (!observing) return result;

    // Only documented driver-message parameters are interpreted. Never inspect
    // the ACM's reserved tail fields or private driver/manager allocation layout.
    AcquireSRWLockExclusive(&gate);
    if (openInput && floatingPointObserved && result == MMSYSERR_NOERROR && instance->has &&
        !(opening.flags & ACM_STREAMOPENF_QUERY) && entryEpoch == stats[6] && !stats[11]) {
        Slot* selected = Find(instance->has);
        if (!selected) for (auto& slot : slots) if (!slot.stream) { selected = &slot; break; }
        if (selected) {
            *selected = opening;
            selected->stream = instance->has; selected->instance = instance;
            selected->token = ++serial; selected->epoch = stats[6]; ++stats[4];
        } else ++stats[7];
    } else if ((message == ACMDM_STREAM_CONVERT || message == ACMDM_STREAM_RESET || message == ACMDM_STREAM_CLOSE) &&
        instance && instance->cbStruct >= sizeof(ACMDRVSTREAMINSTANCE)) {
        Slot* selected = Find(instance->has);
        if (selected && selected->instance == instance) {
            if (message == ACMDM_STREAM_RESET) ++selected->resets;
            if (message == ACMDM_STREAM_CONVERT) {
                ++selected->converts;
                if (scope.active && scope.stream == instance->has) {
                    auto* header = reinterpret_cast<PACMDRVSTREAMHEADER>(second);
                    auto& r = scope.receipt;
                    ++r[AcmOwnedCalls]; r[AcmResult] = static_cast<uint64_t>(result);
                    r[AcmStreamInstance] = reinterpret_cast<uintptr_t>(instance);
                    r[AcmDriverId] = driverId; r[AcmDriverHandle] = reinterpret_cast<uintptr_t>(driverHandle);
                    r[AcmResetCalls] = selected->resets; r[AcmConvertCalls] = selected->converts;
                    const bool matching = header && header->cbStruct >= sizeof(ACMSTREAMHEADER) &&
                        header->pbSrc == scope.source && header->pbDst == scope.destination &&
                        header->cbSrcLength == scope.sourceLength && header->cbDstLength == scope.destinationLength;
                    if (matching) {
                        r[AcmSourceUsed] = header->cbSrcLengthUsed; r[AcmDestinationUsed] = header->cbDstLengthUsed;
                        r[AcmConvertFlags] = header->fdwConvert;
                    }
                    const bool valid = matching && result == MMSYSERR_NOERROR && r[AcmOwnedCalls] == 1 &&
                        scope.token == selected->token && scope.epoch == stats[6] && !stats[11] &&
                        floatingPointObserved && (scope.floatingPoint & ~1ULL) == (entryFloatingPoint & ~1ULL) &&
                        header->cbSrcLengthUsed <= scope.sourceLength && header->cbDstLengthUsed <= scope.destinationLength;
                    r[AcmMatched] = valid ? 1 : 0;
                    if (valid) { selected->witnessed = true; selected->floatingPoint = entryFloatingPoint; ++stats[5]; }
                }
            }
            if (message == ACMDM_STREAM_CLOSE && result == MMSYSERR_NOERROR) *selected = Slot{};
        }
    }
    if (configuration) --stats[11];
    --stats[2]; WakeAllConditionVariable(&changed);
    ReleaseSRWLockExclusive(&gate);
    return result;
}

const unsigned char* DiskAt(const std::vector<unsigned char>& file, const IMAGE_NT_HEADERS64* pe,
    uint32_t rva, size_t size) {
    const auto* sections = IMAGE_FIRST_SECTION(pe);
    for (WORD i = 0; i < pe->FileHeader.NumberOfSections; ++i) {
        const auto& section = sections[i];
        if (rva >= section.VirtualAddress && static_cast<uint64_t>(rva) + size <=
            static_cast<uint64_t>(section.VirtualAddress) + section.SizeOfRawData) {
            const uint64_t offset = static_cast<uint64_t>(section.PointerToRawData) + rva - section.VirtualAddress;
            if (offset + size <= file.size()) return file.data() + static_cast<size_t>(offset);
        }
    }
    return nullptr;
}

bool EquivalentLoaderJump(const std::vector<unsigned char>& file, const IMAGE_NT_HEADERS64* pe,
    HMODULE module, bool math) {
    // Exact SHA-pinned DVRT symbol7 / X64_REL32 declarations: provider .reloc
    // section8+0xC8 names F011; CRT section8+0xB28 names F8011. Their originals
    // are E2E0 and ED6A0 respectively. SDK winnt.h IMAGE_FUNCTION_OVERRIDE_*.
    // CRT's other overrides are validated separately against their finite exact
    // DVRT alternative lists, and their selected RVAs enter persistent identity.
    const uint32_t operandRva = math ? 0xf8011U : 0xf011U;
    const uint32_t thunkRva = math ? 0xed6a0U : 0xe2e0U;
    const uint32_t slotRva = math ? 0xfdba8U : 0x10648U;
    const auto* jump = DiskAt(file, pe, operandRva - 1, 5);
    const auto* thunk = DiskAt(file, pe, thunkRva, 6);
    const auto* configBytes = DiskAt(file, pe,
        pe->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG].VirtualAddress,
        sizeof(IMAGE_LOAD_CONFIG_DIRECTORY64));
    if (!jump || !thunk || !configBytes) return false;
    const auto* config = reinterpret_cast<const IMAGE_LOAD_CONFIG_DIRECTORY64*>(configBytes);
    const auto* mapped = reinterpret_cast<const unsigned char*>(module);
    int32_t diskDisplacement = 0, mappedDisplacement = 0, slotDisplacement = 0;
    std::memcpy(&diskDisplacement, jump + 1, sizeof(diskDisplacement));
    std::memcpy(&mappedDisplacement, mapped + operandRva, sizeof(mappedDisplacement));
    std::memcpy(&slotDisplacement, thunk + 2, sizeof(slotDisplacement));
    const int64_t target = static_cast<int64_t>(reinterpret_cast<uintptr_t>(module)) + operandRva + 4 + mappedDisplacement;
    const uint64_t dispatcher = *reinterpret_cast<const uint64_t*>(mapped + slotRva);
    uint64_t bytes = 0;
    for (unsigned int i = 0; i < 5; ++i) bytes |= static_cast<uint64_t>(mapped[operandRva - 1 + i]) << (i * 8);
    MEMORY_BASIC_INFORMATION targetRegion{};
    uint64_t targetBytes[2]{}; SIZE_T targetRead = 0;
    const auto* targetAddress = reinterpret_cast<const void*>(static_cast<uintptr_t>(target));
    if (target > 0 && VirtualQuery(targetAddress, &targetRegion, sizeof(targetRegion)) &&
        targetRegion.State == MEM_COMMIT && targetRegion.Protect == PAGE_EXECUTE_READ) {
        if (!ReadProcessMemory(GetCurrentProcess(), targetAddress, targetBytes, sizeof(targetBytes), &targetRead))
            targetRead = 0;
    }
    AcquireSRWLockExclusive(&gate);
    stats[15] = bytes; stats[16] = static_cast<uint64_t>(target); stats[17] = dispatcher;
    stats[37] = reinterpret_cast<uintptr_t>(targetRegion.BaseAddress);
    stats[38] = reinterpret_cast<uintptr_t>(targetRegion.AllocationBase);
    stats[39] = targetRegion.State; stats[40] = targetRegion.Type; stats[41] = targetRegion.Protect;
    stats[42] = targetRead; stats[43] = targetBytes[0]; stats[44] = targetBytes[1];
    ReleaseSRWLockExclusive(&gate);
    const bool declaredChain = jump[0] == 0xe9 && mapped[operandRva - 1] == 0xe9 &&
        static_cast<int64_t>(operandRva) + 4 + diskDisplacement == thunkRva &&
        thunk[0] == 0xff && thunk[1] == 0x25 &&
        static_cast<int64_t>(thunkRva) + 6 + slotDisplacement == slotRva &&
        config->GuardCFDispatchFunctionPointer == pe->OptionalHeader.ImageBase + slotRva &&
        std::memcmp(mapped + thunkRva, thunk, 6) == 0 && dispatcher;
    bool equivalent = declaredChain && static_cast<uint64_t>(target) == dispatcher;
    if (declaredChain && !equivalent && target > 0) {
        // An exact loader-selected, complete instruction may replace the local
        // fallback: FF E0 and 48 FF E0 both jump to RAX in x64 without changing
        // registers, flags or stack. No external arithmetic/call path is admitted.
        const uintptr_t base = reinterpret_cast<uintptr_t>(module);
        const uint64_t localRva = dispatcher >= base ? dispatcher - base : UINT64_MAX;
        const auto* local = localRva <= UINT32_MAX ? DiskAt(file, pe, static_cast<uint32_t>(localRva), 2) : nullptr;
        const auto* sections = IMAGE_FIRST_SECTION(pe);
        bool localText = false;
        for (WORD i = 0; i < pe->FileHeader.NumberOfSections; ++i) {
            const auto& section = sections[i];
            if (std::memcmp(section.Name, ".text\0\0\0", 8) == 0 && (section.Characteristics & IMAGE_SCN_MEM_EXECUTE) &&
                localRva >= section.VirtualAddress && localRva + 2 <= static_cast<uint64_t>(section.VirtualAddress) + section.Misc.VirtualSize)
                localText = true;
        }
        MEMORY_BASIC_INFORMATION region{};
        const auto* actual = reinterpret_cast<const unsigned char*>(static_cast<uintptr_t>(target));
        const HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
        const bool queriedTarget = VirtualQuery(actual, &region, sizeof(region)) != 0;
        // Windows can place this same complete JMP RAX in a loader-added image
        // page immediately after the pinned image, instead of using NTDLL.
        // It must remain part of this retained allocation, not adjacent private
        // memory. Exact instruction equivalence is still required below.
        const bool ownLoaderPage = queriedTarget && region.AllocationBase == module &&
            reinterpret_cast<uintptr_t>(region.BaseAddress) == base + pe->OptionalHeader.SizeOfImage &&
            static_cast<uintptr_t>(target) >= base + pe->OptionalHeader.SizeOfImage;
        const bool knownAllocation = queriedTarget && ((ntdll && region.AllocationBase == ntdll) || ownLoaderPage);
        const bool mappedTarget = knownAllocation &&
            region.State == MEM_COMMIT && region.Protect == PAGE_EXECUTE_READ && region.Type == MEM_IMAGE &&
            region.RegionSize >= 3 &&
            static_cast<uintptr_t>(target) >= reinterpret_cast<uintptr_t>(region.BaseAddress) &&
            static_cast<uintptr_t>(target) - reinterpret_cast<uintptr_t>(region.BaseAddress) <= region.RegionSize - 3;
        equivalent = localText && local && local[0] == 0xff && local[1] == 0xe0 &&
            std::memcmp(reinterpret_cast<const void*>(static_cast<uintptr_t>(dispatcher)), local, 2) == 0 &&
            mappedTarget && actual[0] == 0x48 && actual[1] == 0xff && actual[2] == 0xe0;
        if (equivalent) {
            const size_t index = math ? 1 : 0;
            equivalentCfgTarget[index] = static_cast<uintptr_t>(target);
            equivalentCfgAllocation[index] = region.AllocationBase;
            equivalentCfgSlot[index] = base + slotRva;
            equivalentCfgDispatcher[index] = dispatcher;
        }
    }
    if (equivalent) {
        const size_t index = math ? 1 : 0;
        equivalentCfgSlot[index] = reinterpret_cast<uintptr_t>(module) + slotRva;
        equivalentCfgDispatcher[index] = dispatcher;
        AcquireSRWLockExclusive(&gate); ++stats[18]; ReleaseSRWLockExclusive(&gate);
    }
    return equivalent;
}

bool BindMathOverrides(const std::vector<unsigned char>& file, const IMAGE_NT_HEADERS64* pe, HMODULE module) {
    const auto* mapped = reinterpret_cast<const unsigned char*>(module);
    uint64_t identity[16]{}; identity[0] = 1; identity[1] = 13;
    uint64_t changedCount = 0;
    for (size_t i = 0; i < 13; ++i) {
        const auto& entry = mathOverrides[i];
        const auto* disk = DiskAt(file, pe, entry.operand - 1, 5);
        int32_t originalDisplacement = 0, selectedDisplacement = 0;
        if (!disk) return Refuse(213, entry.operand);
        std::memcpy(&originalDisplacement, disk + 1, 4);
        std::memcpy(&selectedDisplacement, mapped + entry.operand, 4);
        const int64_t diskTarget = static_cast<int64_t>(entry.operand) + 4 + originalDisplacement;
        const int64_t selected = static_cast<int64_t>(entry.operand) + 4 + selectedDisplacement;
        if (disk[0] != 0xe9 || mapped[entry.operand - 1] != 0xe9 || diskTarget != entry.original ||
            (selected != entry.original && selected != entry.alternate)) return Refuse(214, entry.operand);
        // All declared original/alternate RVAs are within the pinned .text,
        // whose complete bytes are still verified below. No external target.
        if (selected < 0x1000 || selected >= 0xf7163) return Refuse(215, entry.operand);
        identity[i + 2] = static_cast<uint64_t>(selected);
        if (std::memcmp(disk, mapped + entry.operand - 1, 5) != 0) ++changedCount;
        std::memcpy(selectedMathJumps[i], mapped + entry.operand - 1, 5);
    }
    AcquireSRWLockExclusive(&gate);
    std::memcpy(overrideIdentity, identity, sizeof(identity)); stats[18] += changedCount;
    mathJumpsBound = true;
    ReleaseSRWLockExclusive(&gate);
    return true;
}

bool FileAndMappedCode(const wchar_t* path, HMODULE& module, bool math = false) {
    static const unsigned char providerHash[32] = {
        0x5d,0x7f,0x59,0x61,0x41,0x1f,0xe6,0x4b,0x2b,0xc4,0x82,0x97,0xe7,0xc4,0x24,0xaa,
        0xf1,0x92,0x72,0xaa,0x81,0x42,0x87,0xb0,0xa4,0xff,0x1c,0x46,0x9b,0x8b,0xd3,0xdf };
    static const unsigned char mathHash[32] = {
        0x5c,0x52,0xe3,0xa3,0x03,0xba,0xaa,0xc0,0xe0,0xaf,0x8b,0xd9,0xb9,0x61,0x34,0x99,
        0x3d,0xa3,0x4b,0xc9,0xd8,0x34,0xa3,0x1e,0xf3,0x7e,0x1d,0x2c,0xdc,0x7f,0xe1,0x92 };
    const auto* expected = math ? mathHash : providerHash;
    const uint64_t reason = math ? 200 : 100;
    HANDLE& fileHandle = math ? retainedMathFile : retainedFile;
    fileHandle = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (fileHandle == INVALID_HANDLE_VALUE) return Refuse(reason, GetLastError());
    LARGE_INTEGER length{};
    if (!GetFileSizeEx(fileHandle, &length)) return Refuse(reason + 1, GetLastError());
    if (length.QuadPart != (math ? 1377512 : 118784)) return Refuse(reason + 2, static_cast<uint64_t>(length.QuadPart));
    std::vector<unsigned char> file(static_cast<size_t>(length.QuadPart));
    DWORD read = 0;
    if (!ReadFile(fileHandle, file.data(), static_cast<DWORD>(file.size()), &read, nullptr)) return Refuse(reason + 3, GetLastError());
    if (read != file.size()) return Refuse(reason + 4, read);
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    const auto opened = BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0);
    if (opened < 0) return Refuse(reason + 5, static_cast<uint32_t>(opened));
    unsigned char digest[32]{};
    const auto status = BCryptHash(algorithm, nullptr, 0, file.data(), static_cast<ULONG>(file.size()), digest, sizeof(digest));
    BCryptCloseAlgorithmProvider(algorithm, 0);
    if (status < 0) return Refuse(reason + 6, static_cast<uint32_t>(status));
    if (std::memcmp(digest, expected, sizeof(digest)) != 0) return Refuse(reason + 7);
    if (!math) module = LoadLibraryExW(path, nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!module) return Refuse(reason + 8, GetLastError());
    wchar_t loaded[MAX_PATH]{};
    const DWORD pathLength = GetModuleFileNameW(module, loaded, MAX_PATH);
    if (!pathLength || pathLength >= MAX_PATH) return Refuse(reason + 9, GetLastError());
    if (_wcsicmp(path, loaded) != 0) return Refuse(reason + 10);
    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(file.data());
    const auto* pe = reinterpret_cast<const IMAGE_NT_HEADERS64*>(file.data() + dos->e_lfanew);
    const auto* sections = IMAGE_FIRST_SECTION(pe);
    if (math && !BindMathOverrides(file, pe, module)) return false;
    // These exact files have no executable base relocations, but do declare a
    // dynamic rel32 override for the CFG jump. Accept only a direct jump to the
    // same target as the original FF25 chain; every other byte remains exact.
    bool normalized = false;
    const uint32_t fixupRva = math ? 0xf8011U : 0xf011U;
    for (WORD i = 0; i < pe->FileHeader.NumberOfSections; ++i) {
        const auto& section = sections[i];
        if (section.Characteristics & IMAGE_SCN_MEM_EXECUTE) {
            const auto* disk = file.data() + section.PointerToRawData;
            const auto* mapped = reinterpret_cast<const unsigned char*>(module) + section.VirtualAddress;
            if (std::memcmp(disk, mapped, section.SizeOfRawData) != 0) {
                for (DWORD offset = 0; offset < section.SizeOfRawData; ++offset) {
                    if (disk[offset] == mapped[offset]) continue;
                    const uint64_t rva = static_cast<uint64_t>(section.VirtualAddress) + offset;
                    if (rva >= fixupRva && rva < static_cast<uint64_t>(fixupRva) + 4) {
                        if (!normalized) normalized = EquivalentLoaderJump(file, pe, module, math);
                        if (normalized) continue;
                    }
                    bool selectedOverride = false;
                    if (math && mathJumpsBound) for (const auto& entry : mathOverrides)
                        if (rva >= entry.operand && rva < static_cast<uint64_t>(entry.operand) + 4) { selectedOverride = true; break; }
                    if (selectedOverride) continue;
                    return Refuse(reason + 11, rva);
                }
            }
        }
    }
    std::memcpy(selectedCfgJumps[math ? 1 : 0], reinterpret_cast<const unsigned char*>(module) + fixupRva - 1, 5);
    cfgJumpsBound[math ? 1 : 0] = true;
    return true;
}
bool BindMath() {
    constexpr uintptr_t slotsRva[] = { 0x105d8, 0x105e0 };
    constexpr uintptr_t functionsRva[] = { 0x79bf0, 0x7a7e0 };
    for (size_t i = 0; i != 2; ++i) {
        mathTargets[i] = *reinterpret_cast<void* const*>(reinterpret_cast<unsigned char*>(retainedModule) + slotsRva[i]);
        HMODULE module = nullptr;
        if (!mathTargets[i]) return Refuse(300, slotsRva[i]);
        if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
            reinterpret_cast<LPCWSTR>(mathTargets[i]), &module)) return Refuse(301, (static_cast<uint64_t>(slotsRva[i]) << 32) | GetLastError());
        if (mathTargets[i] != reinterpret_cast<unsigned char*>(module) + functionsRva[i])
            return Refuse(302, reinterpret_cast<uintptr_t>(mathTargets[i]) - reinterpret_cast<uintptr_t>(module));
        if (!i) retainedMathModule = module;
        else if (retainedMathModule != module) return Refuse(303, i);
    }
    wchar_t expectedPath[MAX_PATH]{};
    const UINT count = GetSystemDirectoryW(expectedPath, MAX_PATH);
    if (!count || count >= MAX_PATH - 16 || wcscat_s(expectedPath, L"\\ucrtbase.dll") != 0) return Refuse(304, GetLastError());
    if (!FileAndMappedCode(expectedPath, retainedMathModule, true)) return false;
    const auto target = GetProcAddress(retainedMathModule, "_get_FMA3_enable");
    if (reinterpret_cast<void*>(target) != reinterpret_cast<unsigned char*>(retainedMathModule) + 0xcc390)
        return Refuse(305, target ? reinterpret_cast<uintptr_t>(target) - reinterpret_cast<uintptr_t>(retainedMathModule) : 0);
    getFma = reinterpret_cast<GetFma>(target);
    nativeErrno = reinterpret_cast<ErrnoPointer>(GetProcAddress(retainedMathModule, "_errno"));
    nativeDosErrno = reinterpret_cast<DosErrnoPointer>(GetProcAddress(retainedMathModule, "__doserrno"));
    if (!nativeDosErrno) nativeDosErrno = reinterpret_cast<DosErrnoPointer>(GetProcAddress(retainedMathModule, "_doserrno"));
    historicalWrapper = reinterpret_cast<PowWrapper>(reinterpret_cast<unsigned char*>(retainedMathModule) + 0x5e910);
    if (!nativeErrno || !nativeDosErrno) return Refuse(307);
    FpImage image{}; wakeup_fp_save(&image);
    std::memcpy(&supportedCsrMask, image.bytes + 28, 4);
    if (!supportedCsrMask) supportedCsrMask = 0xffbfU;
    int cpu[4]{}; __cpuid(cpu, 1);
    constexpr int features = (1 << 12) | (1 << 19) | (1 << 27) | (1 << 28);
    bothBranchesAvailable = (cpu[2] & features) == features && (_xgetbv(0) & 6) == 6;
    return MathCurrent() || Refuse(306);
}
} // namespace

int __cdecl wakeup_acm_install(const wchar_t* path) {
    AcquireSRWLockExclusive(&gate);
    if (installAttempted) { const int result = stats[0] ? 1 : 0; ReleaseSRWLockExclusive(&gate); return result; }
    installAttempted = true;
    ReleaseSRWLockExclusive(&gate);
    uint64_t error = 1;
    try {
        wchar_t expectedPath[MAX_PATH]{};
        const UINT count = GetSystemDirectoryW(expectedPath, MAX_PATH);
        if (path && count && count < MAX_PATH - 16 &&
            wcscat_s(expectedPath, L"\\l3codeca.acm") == 0 && _wcsicmp(path, expectedPath) == 0) {
            error = 2;
            if (FileAndMappedCode(path, retainedModule)) {
                error = 6;
                if (!BindMath()) throw 0;
                AcquireSRWLockExclusive(&gate); stats[12] = 1; ReleaseSRWLockExclusive(&gate);
                error = 3;
                auto* entry = reinterpret_cast<void*>(GetProcAddress(retainedModule, "DriverProc"));
                HMODULE pin = nullptr, self = nullptr;
                if (entry == reinterpret_cast<unsigned char*>(retainedModule) + EntryRva) {
                    error = 4;
                    const bool providerPinned = GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                        reinterpret_cast<LPCWSTR>(entry), &pin) != 0;
                    if (!providerPinned) Refuse(401, GetLastError());
                    const bool selfPinned = providerPinned && GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                        reinterpret_cast<LPCWSTR>(&wakeup_acm_install), &self) != 0;
                    if (providerPinned && !selfPinned) Refuse(402, GetLastError());
                    if (selfPinned) {
                        error = 5;
                        MH_STATUS status = MH_Initialize();
                        if (status != MH_OK) Refuse(410, static_cast<uint64_t>(status));
                        if (status == MH_OK) {
                            status = MH_CreateHook(entry, reinterpret_cast<void*>(&Observe), reinterpret_cast<void**>(&original));
                            if (status != MH_OK) Refuse(411, static_cast<uint64_t>(status));
                        }
                        void* fmaTarget = reinterpret_cast<unsigned char*>(retainedMathModule) + 0x5e990;
                        void* sseTarget = reinterpret_cast<unsigned char*>(retainedMathModule) + 0x9dd10;
                        if (status == MH_OK) status = MH_CreateHook(mathTargets[0], reinterpret_cast<void*>(&ObservePow), reinterpret_cast<void**>(&originalPow));
                        if (status == MH_OK) status = MH_CreateHook(fmaTarget, reinterpret_cast<void*>(&ObserveFma), reinterpret_cast<void**>(&originalFma));
                        if (status == MH_OK) status = MH_CreateHook(sseTarget, reinterpret_cast<void*>(&ObserveSse), reinterpret_cast<void**>(&originalSse));
                        if (status != MH_OK) Refuse(413, static_cast<uint64_t>(status));
                        if (status == MH_OK) {
                            AcquireSRWLockExclusive(&gate); stats[1] = 1; ReleaseSRWLockExclusive(&gate);
                            status = MH_QueueEnableHook(entry);
                            if (status == MH_OK) status = MH_QueueEnableHook(mathTargets[0]);
                            if (status == MH_OK) status = MH_QueueEnableHook(fmaTarget);
                            if (status == MH_OK) status = MH_QueueEnableHook(sseTarget);
                            if (status == MH_OK) status = MH_ApplyQueued();
                            if (status != MH_OK) Refuse(412, static_cast<uint64_t>(status));
                        }
                        AcquireSRWLockExclusive(&gate);
                        stats[10] = static_cast<uint64_t>(status);
                        if (status == MH_OK) { stats[0] = 1; stats[27] = 1; error = 0; } else stats[1] = 0;
                        ReleaseSRWLockExclusive(&gate);
                    }
                } else Refuse(400, entry ? reinterpret_cast<uintptr_t>(entry) - reinterpret_cast<uintptr_t>(retainedModule) : 0);
            }
        } else Refuse(403, count);
    } catch (...) {
        // Preserve a specific refusal (BindMath uses this exit) over the generic
        // unexpected C++ installation exception diagnostic.
        AcquireSRWLockExclusive(&gate);
        if (!stats[13]) { stats[13] = 900; stats[14] = error; }
        ReleaseSRWLockExclusive(&gate);
    }
    AcquireSRWLockExclusive(&gate); stats[9] = error; ReleaseSRWLockExclusive(&gate);
    // All successful mappings/trampolines remain allocated, including refusal
    // after partial installation. Never remove a potentially executing hook.
    return error == 0 ? 1 : 0;
}

int __cdecl wakeup_acm_begin(void* stream, void* source, uint32_t sourceLength, void* destination,
    uint32_t destinationLength, const void* sourceFormat, uint32_t sourceFormatLength,
    const void* destinationFormat, uint32_t destinationFormatLength) {
    if (scope.active || !stream || !source || !destination) return 0;
    uint64_t floatingPoint = 0;
    if (!MathCurrent() || !FloatingPoint(floatingPoint)) return 0;
    AcquireSRWLockExclusive(&gate);
    Slot* selected = Find(reinterpret_cast<HACMSTREAM>(stream));
    const bool valid = stats[0] && stats[1] && !stats[11] && selected && selected->epoch == stats[6] &&
        !selected->filter && !selected->callback && !(selected->flags & ACM_STREAMOPENF_ASYNC) &&
        (selected->floatingPoint & ~1ULL) == (floatingPoint & ~1ULL) &&
        Equal(selected->source, sourceFormat, sourceFormatLength) && Equal(selected->destination, destinationFormat, destinationFormatLength);
    if (valid) {
        scope = Scope{}; scope.active = true; scope.stream = selected->stream;
        scope.source = source; scope.destination = destination;
        scope.sourceLength = sourceLength; scope.destinationLength = destinationLength;
        scope.token = selected->token; scope.epoch = selected->epoch;
        scope.floatingPoint = floatingPoint;
        auto& r = scope.receipt;
        r[AcmVersion] = 1; r[AcmToken] = selected->token; r[AcmEpoch] = selected->epoch;
        r[AcmStream] = reinterpret_cast<uintptr_t>(stream); r[AcmSource] = reinterpret_cast<uintptr_t>(source);
        r[AcmDestination] = reinterpret_cast<uintptr_t>(destination); r[AcmSourceLength] = sourceLength;
        r[AcmDestinationLength] = destinationLength; r[AcmOpenFlags] = selected->flags;
        r[AcmSourceFormatDigest] = Digest(selected->source); r[AcmDestinationFormatDigest] = Digest(selected->destination);
        r[AcmHasFilter] = selected->filter ? 1 : 0; r[AcmHasCallback] = selected->callback ? 1 : 0;
        r[AcmThread] = GetCurrentThreadId(); r[AcmEntryRva] = EntryRva; r[AcmMappedCodeMatched] = 1;
        r[AcmOpenObserved] = 1; r[AcmSlotCapacity] = Capacity;
        r[AcmFloatingPointControl] = floatingPoint;
    }
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}
int __cdecl wakeup_acm_end(uint64_t* receipt, uint32_t count) {
    if (!scope.active) return 0;
    AcquireSRWLockExclusive(&gate);
    auto& r = scope.receipt;
    r[AcmConfigurationChanged] = scope.epoch != stats[6] || stats[11] ? 1 : 0;
    r[AcmLastConfigurationMessage] = lastConfiguration;
    Slot* selected = Find(scope.stream);
    if (!stats[1] || r[AcmOwnedCalls] != 1 || r[AcmConfigurationChanged] || !selected || selected->token != scope.token) r[AcmMatched] = 0;
    const int matched = r[AcmMatched] && receipt && count == AcmReceiptCount ? 1 : 0;
    if (receipt && count == AcmReceiptCount) std::memcpy(receipt, r, sizeof(r));
    scope = Scope{};
    ReleaseSRWLockExclusive(&gate);
    return matched;
}
int __cdecl wakeup_acm_current(void* stream, uint64_t token) {
    uint64_t floatingPoint = 0;
    if (!MathCurrent() || !FloatingPoint(floatingPoint)) return 0;
    AcquireSRWLockExclusive(&gate);
    const Slot* selected = Find(reinterpret_cast<HACMSTREAM>(stream));
    const bool valid = stats[1] && !stats[11] && selected && selected->token == token && selected->witnessed &&
        selected->epoch == stats[6] && (selected->floatingPoint & ~1ULL) == (floatingPoint & ~1ULL);
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}
int __cdecl wakeup_acm_stop() {
    AcquireSRWLockExclusive(&gate); stats[1] = 0;
    while (stats[2]) SleepConditionVariableSRW(&changed, &gate, INFINITE, 0);
    stats[8] = 1; ReleaseSRWLockExclusive(&gate);
    return 1;
}
uint64_t __cdecl wakeup_acm_stat(int index) {
    if (index < 0 || index >= 45) return 0;
    AcquireSRWLockExclusive(&gate); const uint64_t value = stats[index]; ReleaseSRWLockExclusive(&gate);
    return value;
}

// Caller includes all 16 cells in persistent provider identity. Targets are
// image-relative RVAs, never ASLR-dependent CFG dispatcher addresses.
extern "C" __declspec(dllexport) int __cdecl wakeup_acm_overrides(uint64_t* identity, uint32_t count) {
    AcquireSRWLockExclusive(&gate);
    const bool valid = stats[0] && mathJumpsBound && identity && count == 16;
    if (valid) std::memcpy(identity, overrideIdentity, sizeof(overrideIdentity));
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}

int __cdecl wakeup_acm_current_base(void* stream, uint64_t token) {
    if (!MathCurrent()) return 0;
    AcquireSRWLockExclusive(&gate);
    const Slot* selected = Find(reinterpret_cast<HACMSTREAM>(stream));
    // Historical catch-up is tied to this still-open actual instance/code.
    // The exact provider's observed config/user messages do not mutate decoder
    // arithmetic; a later diagnostic epoch does not erase that native instance.
    const bool valid = stats[1] && selected && selected->token == token && selected->witnessed;
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}
int __cdecl wakeup_acm_math_begin(void* stream, int replay, const uint64_t* context, uint32_t count) {
    if (mathScope.active || !stream || (replay != 0 && replay != 1) || !MathCurrent() ||
        (replay && !ValidMathContext(context, count))) return 0;
    AcquireSRWLockExclusive(&gate);
    const Slot* selected = Find(reinterpret_cast<HACMSTREAM>(stream));
    const bool valid = stats[0] && stats[1] && selected && !selected->filter && !selected->callback &&
        !(selected->flags & ACM_STREAMOPENF_ASYNC) && (replay || (selected->epoch == stats[6] && !stats[11]));
    if (valid) {
        mathScope = MathScope{}; mathScope.active = true; mathScope.replay = replay != 0;
        mathScope.stream = selected->stream;
        if (replay) std::memcpy(mathScope.context, context, sizeof(mathScope.context));
        ++stats[19];
    }
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}
int __cdecl wakeup_acm_math_end(uint64_t* context, uint32_t count) {
    if (!mathScope.active) return 0;
    auto& output = mathScope.output;
    output[0] = 1; output[3] = mathScope.calls;
    output[4] = mathScope.replay ? mathScope.context[4] : mathScope.pairs;
    output[5] = mathScope.replay ? mathScope.context[5] : mathScope.doubles;
    output[6] = mathScope.replay ? mathScope.context[6] : mathScope.suffixes;
    bool valid = mathScope.valid && mathScope.conversions == 1 && ValidMathContext(output, 8);
    if (mathScope.replay && (output[1] != mathScope.context[1] || output[2] != mathScope.context[2] ||
        output[3] != mathScope.context[3] || output[7] != mathScope.context[7])) valid = false;
    if (context && count == 8) std::memcpy(context, output, sizeof(mathScope.output));
    else valid = false;
    AcquireSRWLockExclusive(&gate);
    if (valid) ++stats[20]; else ++stats[25];
    if (mathScope.replay) ++stats[21];
    stats[22] += mathScope.calls; stats[23] += mathScope.doubles; stats[24] += mathScope.suffixes;
    stats[26] = mathScope.refusal;
    mathScope = MathScope{};
    ReleaseSRWLockExclusive(&gate);
    return valid ? 1 : 0;
}
static int CacheRefusal(uint64_t reason, uint64_t expected = 0, uint64_t actual = 0) {
    cacheDiagnostics[3] = reason; cacheDiagnostics[4] = expected; cacheDiagnostics[5] = actual;
    ++cacheDiagnostics[2]; ++cacheDiagnostics[5 + reason];
    return 0;
}
static int TryCached(void* stream, uint64_t token, const uint64_t* context, uint32_t count) {
    const DWORD callerLastError = GetLastError();
    ++cacheDiagnostics[0]; cacheDiagnostics[3] = cacheDiagnostics[4] = cacheDiagnostics[5] = 0;
    if (!ValidMathContext(context, count)) return CacheRefusal(1);
    if (!wakeup_acm_current_base(stream, token)) return CacheRefusal(2);
    AcquireSRWLockExclusive(&gate);
    const Slot* selected = Find(reinterpret_cast<HACMSTREAM>(stream));
    const bool unchanged = selected && selected->token == token && selected->epoch == stats[6] && !stats[11];
    const uint64_t expectedEpoch = selected ? selected->epoch : UINT64_MAX, actualEpoch = stats[6];
    ReleaseSRWLockExclusive(&gate);
    SetLastError(callerLastError);
    if (!unchanged) return CacheRefusal(3, expectedEpoch, actualEpoch);
    const unsigned int current = _mm_getcsr();
    const unsigned int initial = static_cast<unsigned int>(context[1]);
    if ((current & 0xffc0U) != (initial & 0xffc0U)) return CacheRefusal(5, initial & 0xffc0U, current & 0xffc0U);
    if ((current & initial & 0x3fU) != (initial & 0x3fU)) return CacheRefusal(6, initial & 0x3fU, current & 0x3fU);
    // Exception masks are enabled. Sticky flags can only accumulate on the
    // qualified native path. Require all originally preexisting flags already
    // present; then retain any additional caller flags and add native effects.
    _mm_setcsr(current | (static_cast<unsigned int>(context[2]) & 0x3fU));
    ++cacheDiagnostics[1];
    return 1;
}
int __cdecl wakeup_acm_math_try_cached(void* stream, uint64_t token, const uint64_t* context, uint32_t count) {
    if (lastErrorTest.active && !lastErrorTest.boundary) {
        const DWORD outer = GetLastError();
        ++lastErrorTest.receipt[4];
        lastErrorTest.boundary = true;
        int result = 0;
        __try {
            SetLastError(lastErrorTest.sentinel);
            // Re-enter the actual production export, before its caller capture.
            result = wakeup_acm_math_try_cached(stream, token, context, count);
            if (result) ++lastErrorTest.receipt[5];
        }
        __finally {
            if (GetLastError() == lastErrorTest.sentinel) ++lastErrorTest.receipt[6];
            else ++lastErrorTest.receipt[7];
            lastErrorTest.boundary = false;
            SetLastError(outer);
        }
        return result;
    }
    const DWORD callerLastError = GetLastError();
    const int result = TryCached(stream, token, context, count);
    AcquireSRWLockExclusive(&gate);
    ++stats[28]; if (result) ++stats[29]; else ++stats[30]; stats[31] = cacheDiagnostics[3];
    ReleaseSRWLockExclusive(&gate);
    SetLastError(callerLastError);
    return result;
}
int __cdecl wakeup_acm_last_error_test_begin(uint32_t sentinel) {
    const DWORD outer = GetLastError();
    if (lastErrorTest.active || mathScope.active || !stats[0] || (sentinel != 0 && sentinel != 487)) return 0;
    lastErrorTest = LastErrorTest{};
    lastErrorTest.saved = outer; lastErrorTest.sentinel = sentinel; lastErrorTest.active = true;
    lastErrorTest.receipt[0] = 1; lastErrorTest.receipt[1] = sentinel;
    SetLastError(outer);
    return 1;
}
int __cdecl wakeup_acm_last_error_test_end(uint64_t* receipt, uint32_t count) {
    if (!lastErrorTest.active || lastErrorTest.boundary || mathScope.active || !receipt || count != 9) return 0;
    const DWORD outer = lastErrorTest.saved;
    lastErrorTest.active = false;
    SetLastError(outer);
    lastErrorTest.receipt[8] = GetLastError() == outer ? 1 : 0;
    std::memcpy(receipt, lastErrorTest.receipt, sizeof(lastErrorTest.receipt));
    SetLastError(outer);
    return 1;
}

namespace {
SetFma TestSetter() {
    return retainedMathModule ? reinterpret_cast<SetFma>(GetProcAddress(retainedMathModule, "_set_FMA3_enable")) : nullptr;
}
unsigned int RawFma() {
    return *reinterpret_cast<volatile const unsigned int*>(reinterpret_cast<unsigned char*>(retainedMathModule) + 0x139e60);
}
}
// These explicit disposable-fixture controls are never called by cache/replay.
int __cdecl wakeup_acm_math_test_begin(int fma, uint32_t mxcsr, uint64_t* previous, uint32_t count) {
    const DWORD callerLastError = GetLastError();
    if (controlTest.active || mathScope.active || !stats[0] || !getFma || !TestSetter() ||
        fma < -1 || fma > 1 || (mxcsr != UINT32_MAX && (mxcsr & ~supportedCsrMask)) || !previous || count != 2) return 0;
    Capture(controlTest.saved); controlTest.fma = RawFma();
    controlTest.saved.lastError = callerLastError;
    previous[0] = static_cast<uint64_t>(controlTest.fma); previous[1] = Csr(controlTest.saved.fp);
    controlTest.active = true;
    controlTest.aba = controlTest.abaInPow = false;
    std::memset(controlTest.abaReport, 0, sizeof(controlTest.abaReport));
    if (fma >= 0) TestSetter()(fma);
    if (mxcsr != UINT32_MAX) _mm_setcsr(mxcsr);
    return 1;
}
int __cdecl wakeup_acm_math_test_change(int fma, uint32_t mxcsr) {
    if (!controlTest.active || mathScope.active || fma < -1 || fma > 1 ||
        (mxcsr != UINT32_MAX && (mxcsr & ~supportedCsrMask))) return 0;
    if (fma >= 0) TestSetter()(fma);
    if (mxcsr != UINT32_MAX) _mm_setcsr(mxcsr);
    return 1;
}
int __cdecl wakeup_acm_math_test_end() {
    if (!controlTest.active || mathScope.active) return 0;
    TestSetter()(static_cast<int>(controlTest.fma & 1U));
    const bool restored = RawFma() == controlTest.fma;
    controlTest.aba = false; controlTest.abaReport[0] = 0;
    AcquireSRWLockExclusive(&gate);
    for (size_t i = 0; i < 5; ++i) stats[32 + i] += controlTest.abaReport[1 + i];
    ReleaseSRWLockExclusive(&gate);
    Restore(controlTest.saved); controlTest.active = false;
    return restored ? 1 : 0;
}
int __cdecl wakeup_acm_math_cache_diagnostics(uint64_t* receipt, uint32_t count) {
    if (!receipt || count != 12) return 0;
    std::memcpy(receipt, cacheDiagnostics, sizeof(cacheDiagnostics)); return 1;
}
int __cdecl wakeup_acm_math_test_aba(int enabled) {
    if (!controlTest.active || mathScope.active || (enabled != 0 && enabled != 1) || !bothBranchesAvailable) return 0;
    controlTest.aba = enabled != 0; controlTest.abaReport[0] = enabled != 0 ? 1 : 0; return 1;
}
int __cdecl wakeup_acm_math_test_aba_report(uint64_t* receipt, uint32_t count) {
    if (!receipt || count != 8) return 0;
    std::memcpy(receipt, controlTest.abaReport, sizeof(controlTest.abaReport)); return 1;
}
int __cdecl wakeup_acm_math_test_unwind(uint64_t* receipt, uint32_t count) {
    if (!stats[0] || !receipt || count != 4 || mathScope.active) return 0;
    Effects before{}, after{}; Capture(before);
    bool caught = false;
    __try {
        __try {
            _mm_setcsr((Csr(before.fp) & ~0x6000U) | 0x2000U);
            RaiseException(0xe042c110, 0, 0, nullptr);
        }
        __finally { Restore(before); }
    }
    __except (GetExceptionCode() == 0xe042c110 ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH) { caught = true; }
    Capture(after);
    const bool exact = std::memcmp(before.fp.bytes, after.fp.bytes, 160) == 0 &&
        before.error == after.error && before.dos == after.dos && before.lastError == after.lastError;
    receipt[0] = caught ? 1 : 0; receipt[1] = exact ? 1 : 0; receipt[2] = Csr(before.fp); receipt[3] = Csr(after.fp);
    Restore(before);
    return caught && exact ? 1 : 0;
}
