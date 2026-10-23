find_package(GTest REQUIRED)

add_executable(objcd_tests is_objc_object_test.mm get_objc_variable_test.mm)
# CMake's OBJCXX support doesn't recognise recent clang, so build .mm files as C++ with -x objective-c++.
# Conan only supplies gnustep-base's Objective-C flags for C, so repeat them here.
set_source_files_properties(is_objc_object_test.mm get_objc_variable_test.mm PROPERTIES LANGUAGE CXX
  COMPILE_OPTIONS "-x;objective-c++;-fexceptions;-fobjc-exceptions;-fobjc-runtime=gnustep-2.2;-fblocks")
target_link_libraries(objcd_tests PRIVATE objcd GTest::gtest_main gnustep-base::gnustep-base libobjc2::libobjc2)

add_test(NAME objcd_tests COMMAND objcd_tests)

# Equivalent of conanrun: make Conan's shared libraries and objcd.dll findable at test time.
set(_test_path_mods "PATH=path_list_prepend:$<TARGET_FILE_DIR:objcd>")
if(CONAN_RUNTIME_LIB_DIRS)
  list(JOIN CONAN_RUNTIME_LIB_DIRS "\\;" _conan_path)
  list(APPEND _test_path_mods "PATH=path_list_prepend:${_conan_path}")
endif()
set_tests_properties(objcd_tests PROPERTIES ENVIRONMENT_MODIFICATION "${_test_path_mods}")
