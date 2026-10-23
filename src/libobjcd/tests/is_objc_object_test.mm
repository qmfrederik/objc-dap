#include <gtest/gtest.h>

#include <Foundation/Foundation.h>
#include <cstdint>

#include "objcd.h"

TEST(IsObjcObject, NullIsNotAnObject)
{
    EXPECT_FALSE(is_objc_object(nullptr));
}

TEST(IsObjcObject, SmallObjectPointerIsAnObject)
{
    id small_object = [NSNumber numberWithInt:42];
    ASSERT_NE(small_object, nil);
    void* pointer = (__bridge void*)small_object;
    std::uintptr_t bits = reinterpret_cast<std::uintptr_t>(pointer);
    ASSERT_NE(bits & OBJC_SMALL_OBJECT_MASK, std::uintptr_t { 0 }) << "NSNumber was not a small object: " << pointer;
    EXPECT_TRUE(is_objc_object(pointer));
}

TEST(IsObjcObject, NSObjectInstanceIsAnObject)
{
    id obj = [NSObject new];
    ASSERT_NE(obj, nil);
    EXPECT_TRUE(is_objc_object((__bridge void*)obj));
    [obj release];
}

TEST(IsObjcObject, UnreadablePointerIsNotAnObject)
{
    // Aligned, not a small-object pointer, and in the unmapped low page.
    EXPECT_FALSE(is_objc_object(reinterpret_cast<void*>(std::uintptr_t { 0x1000 })));
}

TEST(IsObjcObject, ZeroedMemoryIsNotAnObject)
{
    alignas(16) unsigned char buffer[16] = {};
    EXPECT_FALSE(is_objc_object(buffer));
}

TEST(IsObjcObject, UnregisteredIsaIsNotAnObject)
{
    alignas(16) unsigned char fake_class[16] = {};
    alignas(16) void* fake_object[2] = { fake_class, nullptr };
    EXPECT_FALSE(is_objc_object(fake_object));
}
