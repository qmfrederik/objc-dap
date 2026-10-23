#include <gtest/gtest.h>

#include <Foundation/Foundation.h>
#include <cinttypes>
#include <cstdint>
#include <cstdio>
#include <string>

#include "objcd.h"

TEST(GetObjcVariable, NullReturnsNull)
{
    EXPECT_EQ(get_objc_variable(nullptr), nullptr);
}

TEST(GetObjcVariable, NonObjectReturnsNull)
{
    alignas(16) unsigned char buffer[16] = {};
    EXPECT_EQ(get_objc_variable(buffer), nullptr);
}

TEST(GetObjcVariable, PopulatesValueTypeAndMemoryReference)
{
    NSString* str = [[NSString alloc] initWithUTF8String:"hello"];
    void* pointer = (__bridge void*)str;

    objcd_dap_variable* var = get_objc_variable(pointer);
    ASSERT_NE(var, nullptr);
    EXPECT_STREQ(var->value, "hello");
    EXPECT_NE(std::string(var->type).find("String"), std::string::npos) << var->type;

    char expected[32];
    std::snprintf(expected, sizeof expected, "0x%" PRIxPTR, reinterpret_cast<std::uintptr_t>(pointer));
    EXPECT_STREQ(var->memory_reference, expected);

    free_objc_variable(var);
    [str release];
}

TEST(GetObjcVariable, NSObjectUsesClassNameAndDescription)
{
    id obj = [NSObject new];
    objcd_dap_variable* var = get_objc_variable((__bridge void*)obj);
    ASSERT_NE(var, nullptr);
    EXPECT_STREQ(var->type, "NSObject");
    EXPECT_NE(std::string(var->value).find("NSObject"), std::string::npos) << var->value;
    free_objc_variable(var);
    [obj release];
}

TEST(GetObjcVariable, SmallObjectIsDescribed)
{
    id number = [NSNumber numberWithInt:42];
    objcd_dap_variable* var = get_objc_variable((__bridge void*)number);
    ASSERT_NE(var, nullptr);
    EXPECT_STREQ(var->value, "42");
    free_objc_variable(var);
}
