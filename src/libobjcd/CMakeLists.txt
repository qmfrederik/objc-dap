cmake_minimum_required(VERSION 3.22)

project(objcd LANGUAGES C CXX)

if(WIN32)
  # The Conan toolchain only defines the runtime for Release, leaving RelWithDebInfo without a CRT under -nostdlib.
  set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

find_package(libobjc2)
find_package(gnustep-base)
add_subdirectory(targets)
