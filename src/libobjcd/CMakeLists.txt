cmake_minimum_required(VERSION 3.22)

project(objcd LANGUAGES C CXX)

if(WIN32)
  # The Conan toolchain only defines the runtime for Release, leaving RelWithDebInfo without a CRT under -nostdlib.
  set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

if(UNIX)
  # Conan libraries carry no RUNPATH of their own, and DT_RUNPATH isn't applied to transitive dependencies
  # (e.g. libdispatch via libgnustep-base). DT_RPATH is, so make the linker emit that instead.
  add_link_options("LINKER:--disable-new-dtags")
endif()

find_package(libobjc2)
find_package(gnustep-base)
add_subdirectory(targets)
