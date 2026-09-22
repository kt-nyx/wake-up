cmake_minimum_required(VERSION 3.21)
project(WakeUpEntry LANGUAGES C CXX ASM_MASM)
if(NOT WIN32 OR NOT MSVC OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "WakeUpEntry requires Windows x64 with MSVC")
endif()
add_library(WakeUpMinHook STATIC vendor/minhook/src/buffer.c vendor/minhook/src/hook.c
  vendor/minhook/src/trampoline.c vendor/minhook/src/hde/hde64.c)
set_target_properties(WakeUpMinHook PROPERTIES MSVC_RUNTIME_LIBRARY "MultiThreaded")
target_include_directories(WakeUpMinHook PRIVATE vendor/minhook/include)
target_compile_definitions(WakeUpMinHook PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
add_library(WakeUpEntry SHARED WakeUpEntry.cpp WakeUpAcm.cpp WakeUpFp.asm)
target_link_libraries(WakeUpEntry PRIVATE WakeUpMinHook bcrypt)
set_target_properties(WakeUpEntry PROPERTIES OUTPUT_NAME "mono-profiler-wakeupentry" PREFIX ""
  MSVC_RUNTIME_LIBRARY "MultiThreaded")
target_compile_features(WakeUpEntry PRIVATE cxx_std_17)
target_compile_options(WakeUpEntry PRIVATE "$<$<COMPILE_LANGUAGE:CXX>:/W4;/WX>")
target_compile_definitions(WakeUpEntry PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
add_custom_command(TARGET WakeUpEntry POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    "${CMAKE_CURRENT_SOURCE_DIR}/vendor/minhook/LICENSE.txt"
    "$<TARGET_FILE_DIR:WakeUpEntry>/MinHook-LICENSE.txt")
