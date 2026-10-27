#include <Foundation/Foundation.h>

int main()
{
    // Instantiate one NSArray for each concrete class layout.  See
    // https://github.com/gnustep/libs-base/blob/master/Source/GSArray.m
    // for all details.

    // Immutable arrays: GSInlineArray (elements are stored right after the instance)
    NSArray *emptyArray = @[];
    NSArray *stringArray = @[@"one", @"two", @"three"];
    NSArray *mixedArray = @[@42, @"text", @3.14, [NSNull null]];
    NSArray *nestedArray = @[@[@1, @2], @[@3]];

    // Mutable arrays: GSMutableArray (separately allocated element buffer)
    NSMutableArray *mutableArray = [NSMutableArray arrayWithObjects:@"a", @"b", @"c", nil];
    NSMutableArray *grownArray = [NSMutableArray arrayWithCapacity:1];
    for (int i = 0; i < 20; i++)
    {
        [grownArray addObject:@(i)];
    }

    // Immutable copy of a mutable array
    NSArray *copiedArray = [mutableArray copy];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("emptyArray: 0x%p\n", emptyArray);
    printf("stringArray: 0x%p\n", stringArray);
    printf("mixedArray: 0x%p\n", mixedArray);
    printf("nestedArray: 0x%p\n", nestedArray);
    printf("mutableArray: 0x%p\n", mutableArray);
    printf("grownArray: 0x%p\n", grownArray);
    printf("copiedArray: 0x%p\n", copiedArray);
    printf("---\n");

    getchar();
}
