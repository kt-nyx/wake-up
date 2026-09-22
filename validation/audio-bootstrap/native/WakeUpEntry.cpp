// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Fixture-only Mono entry-order prototype. No sampling or timing collection.
#include <windows.h>
#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cwchar>

namespace {
using Pointer = void*;
using Filter = uint32_t(__cdecl*)(Pointer, Pointer);
using Enter = void(__cdecl*)(Pointer, Pointer, Pointer);
using RuntimeEvent = void(__cdecl*)(Pointer);
using DomainEvent = void(__cdecl*)(Pointer, Pointer);
using CreateProfiler = Pointer(__cdecl*)(Pointer);
using SetFilter = void(__cdecl*)(Pointer, Filter);
using SetEnter = void(__cdecl*)(Pointer, Enter);
using SetRuntimeEvent = void(__cdecl*)(Pointer, RuntimeEvent);
using SetDomainEvent = void(__cdecl*)(Pointer, DomainEvent);
using GetPointer = Pointer(__cdecl*)(Pointer);
using GetName = const char*(__cdecl*)(Pointer);
using GetDomain = Pointer(__cdecl*)();
using GetMethod = Pointer(__cdecl*)(Pointer, const char*, int);
using GetFlags = uint32_t(__cdecl*)(Pointer, uint32_t*);
using Invoke = Pointer(__cdecl*)(Pointer, Pointer, Pointer*, Pointer*);
using GetInt = int32_t(__cdecl*)(Pointer);
using GetUInt = uint32_t(__cdecl*)(Pointer);
using SetDomain = int32_t(__cdecl*)(Pointer, int32_t);
using SetPointer = void(__cdecl*)(Pointer);
using NoArguments = void(__cdecl*)();
using NewHandle = Pointer(__cdecl*)(Pointer, int32_t);

struct Api {
    CreateProfiler create = nullptr;
    SetFilter setFilter = nullptr;
    SetEnter setEnter = nullptr;
    SetRuntimeEvent setShutdownBegin = nullptr, setShutdownEnd = nullptr;
    SetDomainEvent setDomainUnloading = nullptr;
    GetPointer methodClass = nullptr, classImage = nullptr, imageAssembly = nullptr;
    GetPointer assemblyName = nullptr, handleTarget = nullptr, reflectionType = nullptr;
    GetPointer typeClass = nullptr, reflectionAssembly = nullptr, unbox = nullptr;
    GetName methodName = nullptr, className = nullptr, classNamespace = nullptr, assemblySimpleName = nullptr;
    GetDomain currentDomain = nullptr, rootDomain = nullptr;
    GetMethod classMethod = nullptr;
    GetFlags methodFlags = nullptr;
    Invoke invoke = nullptr;
    GetPointer methodSignature = nullptr, signatureReturn = nullptr, objectDomain = nullptr;
    GetUInt parameterCount = nullptr;
    GetInt typeCode = nullptr, domainId = nullptr;
    GetDomain currentContext = nullptr;
    SetDomain setDomain = nullptr;
    SetPointer setContext = nullptr, pushDomain = nullptr, freeHandle = nullptr;
    NoArguments popDomain = nullptr;
    NewHandle newHandle = nullptr;
} api;

// ABI: these indices are shared with the managed fixture observer. All counters
// are correctness observations, with no clocks, sampling or cost measurement.
enum Stat : int {
    InitReady, Bound, Accepting, InFlight, FilterUpdate, FilterReverse, Entries,
    BridgeCalls, BridgeExceptions, StopCompleted, ShutdownSeen, ShutdownInflight,
    InitFailure, BindFailure, RejectedEntries, DomainUnloadingSeen, ReceiptFailure,
    StopRejected, RecoveryCalls, RecoveryFailures,
    FilterDomainUnload, LifecycleEntries, LifecycleCompleted, LifecycleFailures,
    CrossDomainEntries, ContextRestores, ContextFailures, BoundDomainId, RootDomainId,
    BoundIsRoot, LifecycleInFlightAtReturn, DomainUnloadWithoutStop, ManagedProcessExitEntries, StatCount
};
std::atomic<uint64_t> stats[StatCount]{};
std::atomic<int> initialized{0}, binding{0};
// 0: not bound; 1: bridge accepts entries; 2: permanently closed. A single
// transition variable prevents late binding from reopening runtime shutdown.
std::atomic<int> executionState{0};
std::atomic<Pointer> bridge{nullptr}, drainBridge{nullptr}, boundDomain{nullptr};
SRWLOCK waitGate = SRWLOCK_INIT;
CONDITION_VARIABLE idle = CONDITION_VARIABLE_INIT;
thread_local unsigned int entryDepth = 0;
thread_local bool lifecycleLeader = false;
// Separate from ordinary entry counting: concurrent unload notifications share
// one drain/join. They must never wait for their own ordinary-entry count.
std::atomic<int> lifecycleState{0}; // 0 not started, 1 draining, 2 complete
wchar_t receiptPath[32768]{};
wchar_t runtimePath[32768]{};
wchar_t companionPath[32768]{};

template<typename T> bool Resolve(HMODULE module, const char* name, T& target) {
    // FARPROC and these public C API pointers have the same Windows x64 size.
    // memcpy avoids MSVC's unsafe function-pointer-cast warning (/W4 /WX).
    FARPROC address = GetProcAddress(module, name);
    static_assert(sizeof(target) == sizeof(address), "Function pointer ABI changed");
    std::memcpy(&target, &address, sizeof(target));
    return address != nullptr;
}

bool LoadApi(HMODULE module) {
#define API(field, name) if (!Resolve(module, name, api.field)) return false
    API(create, "mono_profiler_create");
    API(setFilter, "mono_profiler_set_call_instrumentation_filter_callback");
    API(setEnter, "mono_profiler_set_method_enter_callback");
    API(setShutdownBegin, "mono_profiler_set_runtime_shutdown_begin_callback");
    API(setShutdownEnd, "mono_profiler_set_runtime_shutdown_end_callback");
    API(setDomainUnloading, "mono_profiler_set_domain_unloading_callback");
    API(methodClass, "mono_method_get_class"); API(methodName, "mono_method_get_name");
    API(classImage, "mono_class_get_image"); API(className, "mono_class_get_name");
    API(classNamespace, "mono_class_get_namespace"); API(imageAssembly, "mono_image_get_assembly");
    API(assemblyName, "mono_assembly_get_name"); API(assemblySimpleName, "mono_assembly_name_get_name");
    API(handleTarget, "mono_gchandle_get_target_v2");
    API(reflectionType, "mono_reflection_type_get_type"); API(typeClass, "mono_class_from_mono_type");
    API(reflectionAssembly, "mono_reflection_assembly_get_assembly");
    API(currentDomain, "mono_domain_get"); API(classMethod, "mono_class_get_method_from_name");
    API(rootDomain, "mono_get_root_domain");
    API(methodFlags, "mono_method_get_flags");
    API(invoke, "mono_runtime_invoke"); API(unbox, "mono_object_unbox");
    API(methodSignature, "mono_method_signature"); API(parameterCount, "mono_signature_get_param_count");
    API(signatureReturn, "mono_signature_get_return_type"); API(typeCode, "mono_type_get_type");
    API(objectDomain, "mono_object_get_domain"); API(domainId, "mono_domain_get_id");
    API(currentContext, "mono_context_get"); API(setContext, "mono_context_set");
    API(setDomain, "mono_domain_set");
    // Exported UNITY_MONO_API domain-reference operations, paired on this thread.
    API(pushDomain, "mono_thread_push_appdomain_ref"); API(popDomain, "mono_thread_pop_appdomain_ref");
    API(newHandle, "mono_gchandle_new_v2"); API(freeHandle, "mono_gchandle_free_v2");
#undef API
    return true;
}

bool Equal(const char* value, const char* expected) { return value != nullptr && std::strcmp(value, expected) == 0; }

bool MethodShape(Pointer method, uint32_t parameters, int32_t result, bool isStatic) {
    Pointer signature = api.methodSignature(method);
    return signature && api.parameterCount(signature) == parameters
        && api.typeCode(api.signatureReturn(signature)) == result
        && ((api.methodFlags(method, nullptr) & 0x10) != 0) == isStatic;
}

bool IsDomainUnload(Pointer method) {
    Pointer type = api.methodClass(method);
    if (!type || !Equal(api.className(type), "AppDomain") || !Equal(api.classNamespace(type), "System")
        || !Equal(api.methodName(method), "DoDomainUnload")) return false;
    Pointer assembly = api.imageAssembly(api.classImage(type));
    return assembly && Equal(api.assemblySimpleName(api.assemblyName(assembly)), "mscorlib")
        && MethodShape(method, 0, 1, false); // MONO_TYPE_VOID, instance
}

uint32_t __cdecl Instrument(Pointer, Pointer method) {
    // Select every matching copy at compile time. Classification is deliberately
    // deferred until execution; a late binding cannot leave an unguarded body.
    if (IsDomainUnload(method)) { stats[FilterDomainUnload].fetch_add(1); return 2; }
    Pointer type = api.methodClass(method);
    if (!type || !Equal(api.className(type), "PatchFunctions") || !Equal(api.classNamespace(type), "HarmonyLib")) return 0;
    Pointer image = api.classImage(type);
    Pointer assembly = image ? api.imageAssembly(image) : nullptr;
    Pointer name = assembly ? api.assemblyName(assembly) : nullptr;
    if (!name || !Equal(api.assemblySimpleName(name), "0Harmony")) return 0;
    const char* methodName = api.methodName(method);
    if (Equal(methodName, "UpdateWrapper")) stats[FilterUpdate].fetch_add(1);
    else if (Equal(methodName, "ReversePatch")) stats[FilterReverse].fetch_add(1);
    else return 0;
    return 2; // MONO_PROFILER_CALL_INSTRUMENTATION_ENTER, pinned public ABI.
}

void FinishEntry() {
    // No native lock spans managed invocation. Pair the wait predicate with its
    // condition variable so explicit stop cannot miss the last departing entry.
    AcquireSRWLockExclusive(&waitGate);
    stats[InFlight].fetch_sub(1);
    WakeAllConditionVariable(&idle);
    ReleaseSRWLockExclusive(&waitGate);
}

void WriteReceipt(const char* stage);

bool Completed(Pointer result) {
    Pointer value = result ? api.unbox(result) : nullptr;
    return value && *static_cast<int32_t*>(value) == 1;
}

// A context is a managed object. Keep it rooted while domain_set temporarily
// replaces the thread's context with the target domain's default context.
struct BridgeContext {
    Pointer caller = nullptr, contextRoot = nullptr;
    bool referenced = false;
};

bool EnterBoundContext(BridgeContext& saved) {
    saved.caller = api.currentDomain();
    Pointer target = boundDomain.load();
    if (!saved.caller || !target) return false;
    if (saved.caller == target) return true;
    Pointer context = api.currentContext();
    if (!context) return false;
    api.pushDomain(saved.caller);
    api.pushDomain(target);
    saved.referenced = true;
    saved.contextRoot = api.newHandle(context, 0);
    if (!saved.contextRoot) return false;
    stats[CrossDomainEntries].fetch_add(1);
    return api.setDomain(target, 0) != 0 && api.currentDomain() == target;
}

bool RestoreContext(BridgeContext& saved) {
    if (!saved.referenced) return saved.caller != nullptr;
    bool ok = api.setDomain(saved.caller, 0) != 0;
    Pointer context = saved.contextRoot ? api.handleTarget(saved.contextRoot) : nullptr;
    if (ok && context) {
        api.setContext(context);
        ok = api.currentDomain() == saved.caller && api.currentContext() == context;
    } else ok = false;
    if (saved.contextRoot) api.freeHandle(saved.contextRoot);
    api.popDomain(); // bound
    api.popDomain(); // caller, only after domain and nondefault context restore
    if (ok) stats[ContextRestores].fetch_add(1);
    return ok;
}

bool InvokeBound(Pointer assembly, bool drainOnly) {
    BridgeContext saved;
    if (!EnterBoundContext(saved)) {
        RestoreContext(saved);
        stats[ContextFailures].fetch_add(1);
        return false;
    }
    Pointer exception = nullptr;
    // Even an image shared with a recognized copy does not establish that the
    // caller domain has the bound domain's managed guards. Zero deliberately
    // selects the existing conservative drain; it is not an assembly identity.
    Pointer argument = saved.referenced ? nullptr : assembly;
    Pointer arguments[] = { &argument };
    if (!drainOnly) stats[BridgeCalls].fetch_add(1);
    Pointer result = api.invoke(drainOnly ? drainBridge.load() : bridge.load(), nullptr,
        drainOnly ? nullptr : arguments, &exception);
    if (exception && !drainOnly) stats[BridgeExceptions].fetch_add(1);
    bool completed = !exception && Completed(result);
    if (!completed && !drainOnly) {
        // An exception-out value is not settlement. A separate hook-free managed
        // bridge must positively confirm that the owned registry has drained.
        stats[RecoveryCalls].fetch_add(1);
        exception = nullptr;
        result = api.invoke(drainBridge.load(), nullptr, nullptr, &exception);
        completed = !exception && Completed(result);
        if (!completed) stats[RecoveryFailures].fetch_add(1);
    }
    if (!RestoreContext(saved)) { stats[ContextFailures].fetch_add(1); return false; }
    return completed;
}

[[noreturn]] void QualificationFailure(const char* stage) {
    executionState.store(2);
    WriteReceipt(stage);
    // Never continue with pending readers or an invalid managed context. This
    // is failed qualification, not an ordinary decoder error outcome.
    TerminateProcess(GetCurrentProcess(), 86);
    for (;;) Sleep(INFINITE);
}

void JoinOrdinaryEntries() {
    executionState.store(2);
    AcquireSRWLockExclusive(&waitGate);
    while (stats[InFlight].load() != 0) SleepConditionVariableSRW(&idle, &waitGate, INFINITE, 0);
    stats[StopCompleted].store(1);
    ReleaseSRWLockExclusive(&waitGate);
}

void LifecycleEntered() {
    stats[LifecycleEntries].fetch_add(1);
    if (!stats[Bound].load()) return;
    if (entryDepth != 0 || lifecycleLeader) {
        stats[LifecycleFailures].fetch_add(1);
        QualificationFailure("reentrant-domain-unload");
    }
    int expected = 0;
    if (lifecycleState.compare_exchange_strong(expected, 1)) {
        lifecycleLeader = true;
        // Do not close native admission before the registry has settled: a new
        // ordinary entry must still wait on that drain, never return inert.
        if (executionState.load() == 1 && !InvokeBound(nullptr, true)) {
            stats[LifecycleFailures].fetch_add(1);
            QualificationFailure("domain-unload-settlement-failure");
        }
        JoinOrdinaryEntries();
        stats[LifecycleInFlightAtReturn].store(stats[InFlight].load());
        stats[LifecycleCompleted].fetch_add(1);
        lifecycleLeader = false;
        AcquireSRWLockExclusive(&waitGate);
        lifecycleState.store(2);
        WakeAllConditionVariable(&idle);
        ReleaseSRWLockExclusive(&waitGate);
    } else {
        // Followers execute no bound-domain code. Their unload cannot start
        // aborting domain users until the leader's drain and join have finished.
        AcquireSRWLockExclusive(&waitGate);
        while (lifecycleState.load() == 1) SleepConditionVariableSRW(&idle, &waitGate, INFINITE, 0);
        ReleaseSRWLockExclusive(&waitGate);
    }
}

void __cdecl MethodEntered(Pointer, Pointer method, Pointer) {
    if (IsDomainUnload(method)) { LifecycleEntered(); return; }
    stats[Entries].fetch_add(1);
    if (executionState.load() != 1) { stats[RejectedEntries].fetch_add(1); return; }
    AcquireSRWLockExclusive(&waitGate);
    bool accepted = executionState.load() == 1;
    if (accepted) stats[InFlight].fetch_add(1);
    ReleaseSRWLockExclusive(&waitGate);
    if (!accepted) { stats[RejectedEntries].fetch_add(1); return; }
    Pointer assembly = api.imageAssembly(api.classImage(api.methodClass(method)));
    ++entryDepth;
    bool completed = InvokeBound(assembly, false);
    --entryDepth;
    FinishEntry();
    if (!completed) QualificationFailure("settlement-contract-failure");
}

void CloseAdmission() {
    executionState.store(2);
    const uint64_t count = stats[InFlight].load();
    uint64_t observed = stats[ShutdownInflight].load();
    while (observed < count && !stats[ShutdownInflight].compare_exchange_weak(observed, count)) { }
}

void WriteReceipt(const char* stage) {
    if (!stats[Bound].load()) return;
    // The bind publishes these immutable buffers before Bound. The runtime owns
    // these shutdown callbacks; no adapter locks or managed callbacks are used.
    char path[32768]{};
    if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, runtimePath, -1, path, static_cast<int>(sizeof(path)), nullptr, nullptr)) {
        stats[ReceiptFailure].fetch_add(1); return;
    }
    char escaped[65536]{};
    size_t output = 0;
    for (size_t i = 0; path[i] != 0; ++i) {
        const unsigned char value = static_cast<unsigned char>(path[i]);
        if (value < 32 || output + 3 >= sizeof(escaped)) { stats[ReceiptFailure].fetch_add(1); return; }
        if (value == '\\' || value == '"') escaped[output++] = '\\';
        escaped[output++] = path[i];
    }
    char receipt[69632]{};
    const int length = std::snprintf(receipt, sizeof(receipt),
        "{\"schema\":\"c10-native-entry-shutdown.v1\",\"stage\":\"%s\",\"runtimeModule\":\"%s\","
        "\"initReady\":%llu,\"bound\":%llu,\"accepting\":%llu,\"inFlight\":%llu,"
        "\"filterUpdate\":%llu,\"filterReverse\":%llu,\"entries\":%llu,\"bridgeCalls\":%llu,"
        "\"bridgeExceptions\":%llu,\"stopCompleted\":%llu,\"shutdownSeen\":%llu,\"shutdownInflight\":%llu,"
        "\"initFailure\":%llu,\"bindFailure\":%llu,\"rejectedEntries\":%llu,"
        "\"domainUnloadingSeen\":%llu,\"receiptFailure\":%llu,\"stopRejected\":%llu,\"recoveryCalls\":%llu,\"recoveryFailures\":%llu,"
        "\"filterDomainUnload\":%llu,\"lifecycleEntries\":%llu,\"lifecycleCompleted\":%llu,\"lifecycleFailures\":%llu,"
        "\"crossDomainEntries\":%llu,\"contextRestores\":%llu,\"contextFailures\":%llu,"
        "\"boundDomainId\":%llu,\"rootDomainId\":%llu,\"boundIsRoot\":%llu,"
        "\"lifecycleInFlightAtReturn\":%llu,\"domainUnloadWithoutStop\":%llu,\"managedProcessExitEntries\":%llu}\n",
        stage, escaped, stats[InitReady].load(), stats[Bound].load(), static_cast<uint64_t>(executionState.load() == 1), stats[InFlight].load(),
        stats[FilterUpdate].load(), stats[FilterReverse].load(), stats[Entries].load(), stats[BridgeCalls].load(),
        stats[BridgeExceptions].load(), stats[StopCompleted].load(), stats[ShutdownSeen].load(), stats[ShutdownInflight].load(),
        stats[InitFailure].load(), stats[BindFailure].load(), stats[RejectedEntries].load(),
        stats[DomainUnloadingSeen].load(), stats[ReceiptFailure].load(), stats[StopRejected].load(), stats[RecoveryCalls].load(), stats[RecoveryFailures].load(),
        stats[FilterDomainUnload].load(), stats[LifecycleEntries].load(), stats[LifecycleCompleted].load(), stats[LifecycleFailures].load(),
        stats[CrossDomainEntries].load(), stats[ContextRestores].load(), stats[ContextFailures].load(),
        stats[BoundDomainId].load(), stats[RootDomainId].load(), stats[BoundIsRoot].load(),
        stats[LifecycleInFlightAtReturn].load(), stats[DomainUnloadWithoutStop].load(), stats[ManagedProcessExitEntries].load());
    if (length < 0 || static_cast<size_t>(length) >= sizeof(receipt)) { stats[ReceiptFailure].fetch_add(1); return; }
    HANDLE file = CreateFileW(receiptPath, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) { stats[ReceiptFailure].fetch_add(1); return; }
    DWORD written = 0;
    const BOOL ok = WriteFile(file, receipt, static_cast<DWORD>(length), &written, nullptr);
    CloseHandle(file);
    if (!ok || written != static_cast<DWORD>(length)) stats[ReceiptFailure].fetch_add(1);
}

void __cdecl ShutdownBegin(Pointer) {
    stats[ShutdownSeen].store(1); CloseAdmission(); WriteReceipt("shutdown-begin");
}
void __cdecl ShutdownEnd(Pointer) {
    stats[ShutdownSeen].store(1); CloseAdmission(); WriteReceipt("shutdown-end");
}
void __cdecl DomainUnloading(Pointer, Pointer domain) {
    if (domain == boundDomain.load()) {
        stats[DomainUnloadingSeen].fetch_add(1);
        if (!stats[StopCompleted].load()) stats[DomainUnloadWithoutStop].fetch_add(1);
        // Pinned runtime raises this only during final domain_free, after the
        // managed DoDomainUnload entry. No managed invocation or join here.
        CloseAdmission();
    }
}
}

extern "C" __declspec(dllexport) void __cdecl mono_profiler_init_wakeupentry(const char*) {
    int expected = 0;
    if (!initialized.compare_exchange_strong(expected, 1)) return;
    HMODULE module = GetModuleHandleW(L"mono-2.0-bdwgc.dll");
    if (!module || !LoadApi(module)) { stats[InitFailure].store(1); return; }
    if (api.rootDomain() != nullptr) { stats[InitFailure].store(6); return; }
    const DWORD length = GetModuleFileNameW(module, runtimePath, static_cast<DWORD>(sizeof(runtimePath) / sizeof(wchar_t)));
    if (!length || length >= sizeof(runtimePath) / sizeof(wchar_t)) { stats[InitFailure].store(2); return; }
    HMODULE companion = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
        reinterpret_cast<LPCWSTR>(&initialized), &companion)) { stats[InitFailure].store(4); return; }
    const DWORD ownLength = GetModuleFileNameW(companion, companionPath, static_cast<DWORD>(sizeof(companionPath) / sizeof(wchar_t)));
    if (!ownLength || ownLength >= sizeof(companionPath) / sizeof(wchar_t)) { stats[InitFailure].store(5); return; }
    Pointer profiler = api.create(nullptr);
    if (!profiler) { stats[InitFailure].store(3); return; }
    api.setFilter(profiler, Instrument);
    api.setEnter(profiler, MethodEntered);
    api.setShutdownBegin(profiler, ShutdownBegin);
    api.setShutdownEnd(profiler, ShutdownEnd);
    api.setDomainUnloading(profiler, DomainUnloading);
    stats[InitReady].store(1);
}

extern "C" __declspec(dllexport) int __cdecl wakeup_entry_bind(Pointer rootedTypeGCHandle, const wchar_t* path) {
    int expected = 0;
    if (!stats[InitReady].load() || stats[ShutdownSeen].load() || !binding.compare_exchange_strong(expected, 1)) {
        stats[BindFailure].fetch_add(1); return 0;
    }
    if (!rootedTypeGCHandle || !path || !path[0] || wcsnlen_s(path, 32768) >= 32768) {
        stats[BindFailure].fetch_add(1); return 0;
    }
    Pointer target = api.handleTarget(rootedTypeGCHandle);
    Pointer type = target ? api.reflectionType(target) : nullptr;
    Pointer klass = type ? api.typeClass(type) : nullptr;
    Pointer method = klass ? api.classMethod(klass, "OnMethodEntry", 1) : nullptr;
    Pointer drain = klass ? api.classMethod(klass, "DrainOnly", 0) : nullptr;
    Pointer domain = api.currentDomain();
    Pointer root = api.rootDomain();
    if (!method || !drain || !domain || !root || !api.currentContext() || api.objectDomain(target) != domain
        || !MethodShape(method, 1, 8, true) || !MethodShape(drain, 0, 8, true)) {
        stats[BindFailure].fetch_add(1); return 0;
    }
    if (wcscpy_s(receiptPath, path) != 0) { stats[BindFailure].fetch_add(1); return 0; }
    boundDomain.store(domain);
    stats[BoundDomainId].store(static_cast<uint32_t>(api.domainId(domain)));
    stats[RootDomainId].store(static_cast<uint32_t>(api.domainId(root)));
    stats[BoundIsRoot].store(domain == root ? 1 : 0);
    bridge.store(method);
    drainBridge.store(drain);
    stats[Bound].store(1);
    expected = 0;
    if (!executionState.compare_exchange_strong(expected, 1)) { stats[BindFailure].fetch_add(1); return 0; }
    return 1;
}

extern "C" __declspec(dllexport) Pointer __cdecl wakeup_entry_assembly(Pointer managedAssemblyGCHandle) {
    if (!stats[InitReady].load() || !managedAssemblyGCHandle) return nullptr;
    Pointer target = api.handleTarget(managedAssemblyGCHandle);
    return target ? api.reflectionAssembly(target) : nullptr;
}

extern "C" __declspec(dllexport) uint64_t __cdecl wakeup_entry_stat(int index) {
    if (index == Accepting) return executionState.load() == 1 ? 1 : 0;
    return index >= 0 && index < StatCount ? stats[index].load() : 0;
}

extern "C" __declspec(dllexport) const wchar_t* __cdecl wakeup_entry_module_path() { return companionPath; }

extern "C" __declspec(dllexport) int __cdecl wakeup_entry_stop() {
    if (entryDepth != 0 || lifecycleLeader) { stats[StopRejected].fetch_add(1); return 0; }
    executionState.store(2);
    AcquireSRWLockExclusive(&waitGate);
    while (stats[InFlight].load() != 0 || lifecycleState.load() == 1)
        SleepConditionVariableSRW(&idle, &waitGate, INFINITE, 0);
    stats[StopCompleted].store(1);
    ReleaseSRWLockExclusive(&waitGate);
    return 1;
}

extern "C" __declspec(dllexport) void __cdecl wakeup_entry_process_exit() {
    stats[ManagedProcessExitEntries].fetch_add(1);
}
