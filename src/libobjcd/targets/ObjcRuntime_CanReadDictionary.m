#include <Foundation/Foundation.h>
#include "common.h"

int main()
{
    // Immutable and mutable dictionaries: GSDictionary and GSMutableDictionary.
    // See https://github.com/gnustep/libs-base/blob/master/Source/GSDictionary.m
    NSDictionary *emptyDictionary = @{};
    NSDictionary *stringDictionary = @{@"one": @"uno", @"two": @"dos", @"three": @"tres"};
    NSMutableDictionary *mutableDictionary = [NSMutableDictionary dictionary];

    // Enough entries to spread over multiple buckets and force the table to grow.
    for (int i = 0; i < 20; i++)
    {
        [mutableDictionary setObject:[NSNumber numberWithInt:i * 10] forKey:[NSString stringWithFormat:@"key%d", i]];
    }

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    PRINT_ADDRESS(emptyDictionary);
    PRINT_ADDRESS(stringDictionary);
    PRINT_ADDRESS(mutableDictionary);
    printf("---\n");

    getchar();
}
