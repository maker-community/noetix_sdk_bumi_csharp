# MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
#
# Builds the flat C ABI over the official noetix_sdk_bumi:
#   libnoetix_bumi_c.so      library functions + HighController + MediaController
#   libnoetix_bumi_low_c.so  library functions + LowController
# They are separate because the official HighController and LowController define
# the same global symbols and cannot be linked together.
#
# The official SDK is NOT part of this repository; point NOETIX_SDK_ROOT at a
# checkout of https://github.com/Noetix-Robotics/noetix_sdk_bumi. Its static
# library contains GCC 11 LTO bytecode, so build with GCC 11 (Ubuntu 22.04).
#
#   cmake -S native -B native/build -DNOETIX_SDK_ROOT=/path/to/noetix_sdk_bumi
#   cmake --build native/build -j
cmake_minimum_required(VERSION 3.16)
project(noetix_bumi_c LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

set(NOETIX_SDK_ROOT "" CACHE PATH "Checkout of the official noetix_sdk_bumi repository")
option(NOETIX_ENABLE_LTO "Mirror the official SDK build (-flto=auto)" ON)

if(NOT NOETIX_SDK_ROOT OR NOT EXISTS "${NOETIX_SDK_ROOT}/include/common.h")
  message(FATAL_ERROR "Set -DNOETIX_SDK_ROOT=<path to noetix_sdk_bumi checkout>")
endif()

if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
  set(NOETIX_ARCH aarch64)
elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
  set(NOETIX_ARCH x86_64)
else()
  message(FATAL_ERROR "Unsupported processor: ${CMAKE_SYSTEM_PROCESSOR}")
endif()

set(NOETIX_STATIC_LIB "${NOETIX_SDK_ROOT}/lib/${NOETIX_ARCH}/libnoetix_sdk.a")
set(NOETIX_DDS_DIR "${NOETIX_SDK_ROOT}/dds/${NOETIX_ARCH}")

find_package(Eigen3 REQUIRED)
find_package(Threads REQUIRED)

function(noetix_bumi_library target)
  add_library(${target} SHARED ${ARGN})

  target_include_directories(${target}
    PUBLIC include
    PRIVATE
      src
      "${NOETIX_SDK_ROOT}/include"
      "${NOETIX_DDS_DIR}/include"
      "${NOETIX_DDS_DIR}/include/ddsc"
      "${NOETIX_DDS_DIR}/include/ddscxx")

  target_link_libraries(${target} PRIVATE
    "${NOETIX_STATIC_LIB}"
    "${NOETIX_DDS_DIR}/lib/libddscxx.so"
    "${NOETIX_DDS_DIR}/lib/libddsc.so"
    Eigen3::Eigen
    Threads::Threads)

  if(NOETIX_ENABLE_LTO)
    target_compile_options(${target} PRIVATE -flto=auto)
    target_link_options(${target} PRIVATE -flto=auto)
  endif()

  target_compile_options(${target} PRIVATE -Wall -Wextra)

  # Find libddsc.so.11 next to the library or in the official SDK checkout.
  set_target_properties(${target} PROPERTIES
    BUILD_RPATH "${NOETIX_DDS_DIR}/lib"
    INSTALL_RPATH "$ORIGIN")

  install(TARGETS ${target} LIBRARY DESTINATION lib)
endfunction()

noetix_bumi_library(noetix_bumi_c
  src/library.cpp src/high.cpp src/media.cpp)

noetix_bumi_library(noetix_bumi_low_c
  src/library.cpp src/low.cpp)

install(FILES include/noetix_bumi_c.h include/noetix_bumi_low_c.h DESTINATION include)
