// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
#pragma once
#include <cstdint>

// All exports use cdecl, Windows x64. No managed callbacks occur in DriverProc.
// Install is one-shot, outside loader/codec callbacks, before native MP3 creation.
// begin/end must run on the SAME thread around exactly one original conversion;
// end belongs in the caller's finally, including when native conversion fails.
// Format buffers contain exactly sizeof(WAVEFORMATEX)+cbSize bytes (PCM: 18).
// current is a provenance/config-generation witness, not general cache admission.
// stop joins observer work; code, trampoline and provider remain process-lifetime.
extern "C" {
__declspec(dllexport) int __cdecl wakeup_acm_install(const wchar_t* exactPath);
__declspec(dllexport) int __cdecl wakeup_acm_begin(void* stream, void* source, uint32_t sourceLength,
    void* destination, uint32_t destinationLength, const void* sourceFormat, uint32_t sourceFormatLength,
    const void* destinationFormat, uint32_t destinationFormatLength);
__declspec(dllexport) int __cdecl wakeup_acm_end(uint64_t* receipt, uint32_t count);
__declspec(dllexport) int __cdecl wakeup_acm_current(void* stream, uint64_t token);
__declspec(dllexport) int __cdecl wakeup_acm_stop();
__declspec(dllexport) uint64_t __cdecl wakeup_acm_stat(int index);
__declspec(dllexport) int __cdecl wakeup_acm_current_base(void* stream, uint64_t token);
__declspec(dllexport) int __cdecl wakeup_acm_math_begin(void* stream, int replay, const uint64_t* context, uint32_t count);
__declspec(dllexport) int __cdecl wakeup_acm_math_end(uint64_t* context, uint32_t count);
__declspec(dllexport) int __cdecl wakeup_acm_math_try_cached(void* stream, uint64_t token, const uint64_t* context, uint32_t count);
// Disposable fixture scope only; sentinel must be 0 or 487. Native boundaries
// restore their own caller LastError in finally, and End restores scope entry.
// Receipt[9]: version,sentinel,nativeCurrentCalls,replayCalls,cacheAttempts,
// cacheHits,preservedBoundaries,mismatches,restoredOuter. The native-current
// count includes unattached original conversions, without creating a math scope.
__declspec(dllexport) int __cdecl wakeup_acm_last_error_test_begin(uint32_t sentinel);
__declspec(dllexport) int __cdecl wakeup_acm_last_error_test_end(uint64_t* receipt, uint32_t count);
// Explicit disposable-fixture controls only: same-thread begin/end in finally.
// fma=-1 and mxcsr=UINT32_MAX leave that control unchanged. previous[2] receives
// exact raw selector flags and complete MXCSR. End restores via the public setter and
// complete native thread FP environment/errno/doserrno/LastError.
__declspec(dllexport) int __cdecl wakeup_acm_math_test_begin(int fma, uint32_t mxcsr, uint64_t* previous, uint32_t count);
__declspec(dllexport) int __cdecl wakeup_acm_math_test_change(int fma, uint32_t mxcsr);
// Explicit control only, no replay/verification writes. Each actual owned pow
// alternates the public setter mode, then restores its exact entry raw selector.
__declspec(dllexport) int __cdecl wakeup_acm_math_test_aba(int enabled);
// armed,calls,actualFMA,actualSSE,setterWrites,restoreFailures,firstRaw,lastRaw.
__declspec(dllexport) int __cdecl wakeup_acm_math_test_aba_report(uint64_t* receipt, uint32_t count);
// Current thread: attempts,hits,refused,lastReason,expected,actual, then reason
// counts1..6: invalid context,base binding,epoch,reserved,controls,sticky flags.
__declspec(dllexport) int __cdecl wakeup_acm_math_cache_diagnostics(uint64_t* receipt, uint32_t count);
__declspec(dllexport) int __cdecl wakeup_acm_math_test_end();
// Owned SEH throw/catch with native finally restoration; receipt[4] is caught,
// full environment+error-state equality, before MXCSR, after MXCSR.
__declspec(dllexport) int __cdecl wakeup_acm_math_test_unwind(uint64_t* receipt, uint32_t count);
}

// Historical math context: 8 uint64 cells (64 bytes).
// [0]=version1; [1]=initial full MXCSR; [2]=final full MXCSR;
// [3]=actual pow calls; [4]=qualified branch pairs; [5]=double-equal pairs;
// [6]=suffix-equal pairs; [7]=entry LastError in high32, SSE RVA 0x9dd10 in low32.
// Admitted conversion leaves LastError unchanged. Cached calls preserve their
// own caller value; historical native replay retains the recorded entry value.
// Record begin ignores input context; replay begin copies a qualified context.
// Both scopes must be ended on the same thread, in caller finally. Temporary
// historical FP state exists only inside owned native DriverProc conversion.

// end's 32 uint64 cells. Result is sign-extended LRESULT, addresses diagnostic only.
enum AcmReceipt : uint32_t {
    AcmVersion, AcmMatched, AcmToken, AcmEpoch, AcmOwnedCalls, AcmResult,
    AcmStream, AcmSource, AcmDestination, AcmSourceLength, AcmDestinationLength,
    AcmSourceUsed, AcmDestinationUsed, AcmConvertFlags, AcmOpenFlags,
    AcmSourceFormatDigest, AcmDestinationFormatDigest, AcmResetCalls, AcmConvertCalls,
    AcmHasFilter, AcmHasCallback, AcmThread, AcmConfigurationChanged, AcmEntryRva,
    AcmMappedCodeMatched, AcmOpenObserved, AcmStreamInstance, AcmDriverId, AcmDriverHandle,
    AcmLastConfigurationMessage, AcmSlotCapacity, AcmFloatingPointControl, AcmReceiptCount
};
// stat indices: installed, accepting, observer-inflight, all-entry-calls,
// successful-opens, successful-owned-converts, configuration-epoch, slot-overflow,
// stop-completed, install-error (1 args,2 file/hash,3 load/map,4 pin,5 MinHook),
// last-MinHook-status, configuration-inflight, bound-math-code-matched.
// install-error 6 means actual pow/sqrt/FMA-query dependency refused.
// Cell31: (_controlfp_s getter << 32) | (MXCSR & 0xffc0) | FMA3-enable (bit0).
// This is checked from native OPEN through first conversion and current().
// No timestamps or timings.
// Stats28..31: cache attempts/hits/refusals/lastReason. Stats32..36: completed
// test-control ABA calls/FMA/SSE/setterWrites/restoreFailures (aggregated at End).
