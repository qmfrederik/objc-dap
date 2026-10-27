#include <Foundation/Foundation.h>

int main()
{
    // Instantiate one NSNumber for each concrete class.  See
    // https://github.com/gnustep/libs-base/blob/master/Source/NSNumber.m
    // for all details.  NSFloatNumber is not covered: on 64-bit every float is boxed as NSSmallFloat.

    // Small objects (value is encoded in the pointer)
    NSNumber *smallInt = @42;                           // NSSmallInt
    NSNumber *smallFloat = @3.14f;                      // NSSmallFloat
    NSNumber *smallRepeatingDouble = @1.0;              // NSSmallRepeatingDouble
    NSNumber *smallExtendedDouble = @3.14;              // NSSmallExtendedDouble

    // Heap objects
    NSNumber *boolNumber = @YES;                        // NSBoolNumber
    NSNumber *reusedInt = @5;                           // NSIntNumber (shared instance for -1..12)
    NSNumber *intNumber = @1234567890;                  // NSIntNumber (too large for NSSmallInt)
    NSNumber *longLongNumber = @5000000000LL;           // NSLongLongNumber
    NSNumber *unsignedLongLongNumber = @18446744073709551615ULL; // NSUnsignedLongLongNumber
    NSNumber *doubleNumber = @2.718281828459045;        // NSDoubleNumber

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("smallInt: 0x%p\n", smallInt);
    printf("smallFloat: 0x%p\n", smallFloat);
    printf("smallRepeatingDouble: 0x%p\n", smallRepeatingDouble);
    printf("smallExtendedDouble: 0x%p\n", smallExtendedDouble);
    printf("boolNumber: 0x%p\n", boolNumber);
    printf("reusedInt: 0x%p\n", reusedInt);
    printf("intNumber: 0x%p\n", intNumber);
    printf("longLongNumber: 0x%p\n", longLongNumber);
    printf("unsignedLongLongNumber: 0x%p\n", unsignedLongLongNumber);
    printf("doubleNumber: 0x%p\n", doubleNumber);
    printf("---\n");

    getchar();
}
