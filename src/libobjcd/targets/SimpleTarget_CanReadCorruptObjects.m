#include <Foundation/Foundation.h>

int main()
{
    NSMutableString *corruptString = [NSMutableString stringWithString:@"Hello"];
    NSMutableArray *corruptArray = [NSMutableArray arrayWithObject:@"item"];
    NSObject *healthy = [NSObject new];

    // The pointer to the characters of the string, and to the elements of the array, follow the isa pointer.
    // Make them point to memory which cannot be read, as they would in an object which is not initialized yet.
    *(void **)((char *)corruptString + sizeof(void *)) = (void *)0x10;
    *(void **)((char *)corruptArray + sizeof(void *)) = (void *)0x10;
    return 0;
}
