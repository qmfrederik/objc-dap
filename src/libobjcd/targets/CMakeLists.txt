add_executable(target target.m)
# Per-target options come after CMAKE_C_FLAGS_<CONFIG>, so -O0 overrides the config's -O level.
target_compile_options(target PRIVATE -O0)
target_link_libraries(target PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(SimpleTarget_CanReadVariables SimpleTarget_CanReadVariables.m)
target_compile_options(SimpleTarget_CanReadVariables PRIVATE -O0)
target_link_libraries(SimpleTarget_CanReadVariables PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(ObjcRuntime_CanReadStrings ObjcRuntime_CanReadStrings.m)
target_compile_options(ObjcRuntime_CanReadStrings PRIVATE -O0)
target_link_libraries(ObjcRuntime_CanReadStrings PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(ObjcRuntime_CanReadNumbers ObjcRuntime_CanReadNumbers.m)
target_compile_options(ObjcRuntime_CanReadNumbers PRIVATE -O0)
target_link_libraries(ObjcRuntime_CanReadNumbers PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(ObjcRuntime_CanReadBaseTypes ObjcRuntime_CanReadBaseTypes.m)
target_compile_options(ObjcRuntime_CanReadBaseTypes PRIVATE -O0)
target_link_libraries(ObjcRuntime_CanReadBaseTypes PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(ObjcRuntime_CanReadArray ObjcRuntime_CanReadArray.m)
target_compile_options(ObjcRuntime_CanReadArray PRIVATE -O0)
target_link_libraries(ObjcRuntime_CanReadArray PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

add_executable(ObjcRuntime_CanReadDictionary ObjcRuntime_CanReadDictionary.m)
target_compile_options(ObjcRuntime_CanReadDictionary PRIVATE -O0)
target_link_libraries(ObjcRuntime_CanReadDictionary PRIVATE gnustep-base::gnustep-base libobjc2::libobjc2)

# Copy required DLLs to the build directory on Windows after building the target executable.
if(WIN32)
    add_custom_command(TARGET target POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${gnustep-base_BIN_DIRS_RELEASE}/gnustep-base-1_31.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libffi_BIN_DIRS_RELEASE}/libffi.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libxslt_BIN_DIRS_RELEASE}/libxslt.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libxml2_BIN_DIRS_RELEASE}/libxml2.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libiconv_BIN_DIRS_RELEASE}/iconv-2.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libcurl_BIN_DIRS_RELEASE}/libcurl.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${openssl_BIN_DIRS_RELEASE}/libcrypto-3-x64.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${openssl_BIN_DIRS_RELEASE}/libssl-3-x64.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${zlib_BIN_DIRS_RELEASE}/zlib1.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libobjc2_BIN_DIRS_RELEASE}/objc.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libdispatch_BIN_DIRS_RELEASE}/dispatch.dll" "${CMAKE_CURRENT_BINARY_DIR}/"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${libdispatch_BIN_DIRS_RELEASE}/blocksRuntime.dll" "${CMAKE_CURRENT_BINARY_DIR}/")
endif(WIN32)
