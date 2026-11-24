#include <Foundation/Foundation.h>
#include "common.h"

int main()
{
    // Class-typed variables, and pointers that are not classes.
    Class objectClass = [NSObject class];
    Class stringClass = [NSString class];
    Class arrayClass = [[NSMutableArray array] class];
    Class metaClass = object_getClass([NSObject class]);
    Class nilClass = Nil;
    id object = [NSObject new];
    void* otherMemory = malloc(64);

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    PRINT_ADDRESS(objectClass);
    PRINT_ADDRESS(stringClass);
    PRINT_ADDRESS(arrayClass);
    PRINT_ADDRESS(metaClass);
    PRINT_ADDRESS(nilClass);
    PRINT_ADDRESS(object);
    PRINT_ADDRESS(otherMemory);
    printf("---\n");

    getchar();
}
