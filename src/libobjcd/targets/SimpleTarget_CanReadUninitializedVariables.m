#include <Foundation/Foundation.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

// Fills the stack with a pattern, so the locals of the function called next hold garbage
// rather than whatever the C runtime happened to leave there.
__attribute__((noinline)) static void poisonStack(uint64_t pattern, int useOwnAddress)
{
    volatile uint64_t buffer[2048];
    for (int i = 0; i < 2048; i++)
    {
        buffer[i] = useOwnAddress ? (uint64_t)(uintptr_t)buffer : pattern;
    }
}

__attribute__((noinline)) static void run(void)
{
    NSString* string;
    NSDictionary* dictionary;
    NSArray* array;
    NSLog(@"The variables are not initialized yet.");
    string = @"Hello, World!";
    dictionary = @{ @"key": @"value" };
    array = @[ @"item1", @"item2" ];
    NSLog(@"%@ %@ %@", string, dictionary, array);
}

// The first argument is the 64-bit pattern (in hex) to fill the stack with, or "self" to fill
// it with the address of the filled memory: a readable address which does not hold an object.
int main(int argc, char** argv)
{
    int useOwnAddress = argc > 1 && strcmp(argv[1], "self") == 0;
    uint64_t pattern = argc > 1 && !useOwnAddress ? strtoull(argv[1], NULL, 16) : 0;
    poisonStack(pattern, useOwnAddress);
    run();
    return 0;
}
