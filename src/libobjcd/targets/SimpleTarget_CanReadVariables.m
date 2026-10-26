#include <Foundation/Foundation.h>

int main()
{
    id small_object = [NSNumber numberWithInt:42];
    id obj = [NSObject new];
    void* otherMemory = malloc(42);
    int number = 42;
    id not_set = nil;

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("small_object: 0x%p\n", small_object);
    printf("obj: 0x%p\n", obj);
    printf("otherMemory: 0x%p\n", otherMemory);
    printf("number: 0x%p\n", number);
    printf("not_set: 0x%p\n", not_set);
    printf("---\n");

    getchar();
}