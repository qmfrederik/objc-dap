#include <Foundation/Foundation.h>

int main()
{
    // Immutable and mutable sets: GSSet and GSMutableSet.
    // See https://github.com/gnustep/libs-base/blob/master/Source/GSSet.m
    NSSet *emptySet = [NSSet set];
    NSSet *stringSet = [NSSet setWithObjects:@"one", @"two", @"three", nil];
    NSMutableSet *mutableSet = [NSMutableSet set];

    // Enough members to spread over multiple buckets and force the table to grow.
    for (int i = 0; i < 20; i++)
    {
        [mutableSet addObject:[NSNumber numberWithInt:i * 10]];
    }

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("emptySet: 0x%p\n", emptySet);
    printf("stringSet: 0x%p\n", stringSet);
    printf("mutableSet: 0x%p\n", mutableSet);
    printf("---\n");

    getchar();
}
