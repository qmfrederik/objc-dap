cmake_minimum_required(VERSION 3.22)

project(objcd LANGUAGES C CXX)

if(WIN32)
  # The Conan toolchain only defines the runtime for Release, leaving RelWithDebInfo without a CRT under -nostdlib.
  set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

find_package(libobjc2)
find_package(gnustep-base)

add_library(objcd SHARED objcd.m)
target_compile_definitions(objcd PRIVATE OBJCD_BUILDING)

# CMake's OBJC language support doesn't recognise recent clang, so build .m files as C with -x objective-c.
set_source_files_properties(objcd.m PROPERTIES LANGUAGE C COMPILE_OPTIONS "-x;objective-c")
target_include_directories(objcd PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(objcd gnustep-base::gnustep-base libobjc2::libobjc2)

if(WIN32)
  # lld-link discards the empty .objcrt$*$a/$z marker sections in a DLL; merging them keeps the start/stop symbols alive.
  target_link_options(objcd PRIVATE LINKER:/merge:.objcrt=.data)
endif()

add_subdirectory(targets)

enable_testing()
add_subdirectory(tests)