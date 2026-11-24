#include <Foundation/Foundation.h>
#include "common.h"

int main()
{
    // Instantiate one NSArray for each concrete class layout.  See
    // https://github.com/gnustep/libs-base/blob/master/Source/GSArray.m
    // for all details.

    // Immutable arrays: GSInlineArray (elements are stored right after the instance)
    NSArray *emptyArray = @[];
    NSArray *stringArray = @[@"one", @"two", @"three"];

    // Mutable arrays: GSMutableArray (separately allocated element buffer)
    NSMutableArray *mutableArray = [NSMutableArray arrayWithObjects:@"a", @"b", @"c", @42, nil];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    PRINT_ADDRESS(emptyArray);
    PRINT_ADDRESS(stringArray);
    PRINT_ADDRESS(mutableArray);
    printf("---\n");

    getchar();
}
