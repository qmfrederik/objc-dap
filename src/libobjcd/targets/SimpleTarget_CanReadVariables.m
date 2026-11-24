#include <Foundation/Foundation.h>
#include "common.h"

int main()
{
    id small_object = [NSNumber numberWithInt:42];
    id obj = [NSObject new];
    void* otherMemory = malloc(42);
    int number = 42;
    id not_set = nil;

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    PRINT_ADDRESS(small_object);
    PRINT_ADDRESS(obj);
    PRINT_ADDRESS(otherMemory);
    PRINT_ADDRESS(number);
    PRINT_ADDRESS(not_set);
    printf("---\n");

    getchar();
}