#include <Foundation/Foundation.h>

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

    printf("objectClass: 0x%p\n", objectClass);
    printf("stringClass: 0x%p\n", stringClass);
    printf("arrayClass: 0x%p\n", arrayClass);
    printf("metaClass: 0x%p\n", metaClass);
    printf("nilClass: 0x%p\n", nilClass);
    printf("object: 0x%p\n", object);
    printf("otherMemory: 0x%p\n", otherMemory);
    printf("---\n");

    getchar();
}
